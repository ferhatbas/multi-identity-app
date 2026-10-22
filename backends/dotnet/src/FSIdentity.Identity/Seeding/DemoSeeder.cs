using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Localization;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Seeding;

public sealed record DemoAccount(string UserName, string FullName, string Tenant, string? Company, string Application, string Rank);

public sealed record DemoSeedResult(IReadOnlyList<DemoAccount> Accounts, string Password);

public class DemoSeeder(AppDbContext db, IdentitySeeder parts, IUserProvisioning provisioning, ApplicationSeeder applications)
{
    public const string ManagerRole = "Manager";
    public const string StaffRole = "Staff";

    public static readonly IReadOnlyList<string> ManagerPermissions =
    [
        Permissions.TenantView, Permissions.UserView, Permissions.RoleView, Permissions.AuditLogView,
        Permissions.UnitManage, Permissions.SettingsManage,
    ];

    public async Task<DemoSeedResult> SeedAsync(DemoDefinition definition, DemoSeedOptions options, CancellationToken cancellationToken = default)
    {
        definition.Validate();
        if (await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
            throw new InvalidOperationException("The database already has users. The demo seed is for an empty database only.");

        using var _ = AuditLogSuppression.Suppress();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await EnsureEnglishIsTheFallbackAsync(cancellationToken);
        await applications.SeedAsync(cancellationToken);
        var admin = await db.AppModules.SingleAsync(m => m.Code == AppModuleCodes.Admin, cancellationToken);
        var appIds = await AddApplicationsAsync(definition, admin, cancellationToken);
        var permissions = await parts.AddPermissionsAsync(cancellationToken);

        var language = options.LanguageCode.Trim().ToLowerInvariant();
        var accounts = new List<DemoAccount>();
        foreach (var (tenantDefinition, isManagement) in definition.Tenants.Select(t => (t, false)).Prepend((definition.Management, true)))
        {
            var tenant = new Tenant { Code = tenantDefinition.Code.Trim().ToUpperInvariant(), Name = tenantDefinition.Name.Trim(), LanguageCode = language };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
            parts.AddTenantSettings(tenant.Id);

            var roles = await AddRolesAsync(tenant.Id, permissions, cancellationToken);
            var applicationRoles = await AddApplicationRolesAsync(tenant.Id, tenantDefinition, definition, appIds, permissions, cancellationToken);

            var companies = new List<(Company Company, DemoCompany Definition, long ApplicationId)>();
            foreach (var c in tenantDefinition.Companies)
            {
                var appCode = c.ApplicationCode.Trim().ToUpperInvariant();
                var company = new Company
                {
                    IdentityTenantId = tenant.Id, Code = c.Code.Trim().ToUpperInvariant(), Name = c.Name.Trim(),
                    CompanyTypeId = appIds[appCode].CompanyTypeId, LanguageCode = language,
                };
                db.Companies.Add(company);
                await db.SaveChangesAsync(cancellationToken);
                parts.AddCompanySettings(tenant.Id, company.Id);
                companies.Add((company, c, appIds[appCode].ApplicationId));
            }

            var first = companies[0];
            var tenantAdmin = await AddUserAsync(tenantDefinition.TenantAdmin!, tenant.Id, first, options, cancellationToken);
            tenantAdmin.IsPlatformAdmin = isManagement;
            foreach (var (company, c, _) in companies)
            {
                Join(tenantAdmin, tenant.Id, company.Id, UserType.TenantAdmin, [roles[IdentitySeeder.AdministratorRole]]);
                accounts.Add(Account(tenantAdmin, tenant, company, c, isManagement ? "Platform administrator" : "Tenant administrator"));
            }

            foreach (var entry in companies)
            {
                var (company, c, _) = entry;
                if (c.CompanyAdmin is { } companyAdmin)
                {
                    var user = await AddUserAsync(companyAdmin, tenant.Id, entry, options, cancellationToken);
                    Join(user, tenant.Id, company.Id, UserType.CompanyAdmin, RolesOf(companyAdmin, c, roles[ManagerRole], roles, applicationRoles));
                    accounts.Add(Account(user, tenant, company, c, "Company administrator"));
                }

                foreach (var person in c.Users)
                {
                    var user = await AddUserAsync(person, tenant.Id, entry, options, cancellationToken);
                    Join(user, tenant.Id, company.Id, UserType.Standard, RolesOf(person, c, roles[StaffRole], roles, applicationRoles));
                    accounts.Add(Account(user, tenant, company, c, "User"));
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        await parts.AddMenuAsync(null, permissions, cancellationToken);
        await parts.AddDashboardAsync(permissions, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new DemoSeedResult(accounts, options.Password);
    }

    private async Task EnsureEnglishIsTheFallbackAsync(CancellationToken cancellationToken)
    {
        var languages = await db.Languages.ToListAsync(cancellationToken);
        var english = languages.FirstOrDefault(l => l.Code == "en");
        if (english is null)
        {
            english = new Language { Code = "en", Name = "English", NativeName = "English", DisplayOrder = 1 };
            db.Languages.Add(english);
        }

        foreach (var other in languages.Where(l => l.IsFallback && l.Code != "en"))
            other.IsFallback = false;
        english.IsFallback = true;
        english.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<string, (long ApplicationId, long CompanyTypeId)>> AddApplicationsAsync(
        DemoDefinition definition, AppModule admin, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);

        var management = await db.ApplicationTypes.SingleAsync(a => a.Code == ApplicationType.ManagementCode, cancellationToken);
        var managementType = new CompanyType { Code = ApplicationType.ManagementCode, Name = "Management", Description = "The vendor's own company" };
        db.CompanyTypes.Add(managementType);

        var order = 10;
        var added = new List<(ApplicationType App, CompanyType Type)>();
        foreach (var a in definition.Applications)
        {
            var app = new ApplicationType { Code = a.Code.Trim().ToUpperInvariant(), Name = a.Name.Trim(), Description = a.Description, DisplayOrder = order += 10 };
            var type = new CompanyType { Code = app.Code, Name = app.Name, Description = a.Description };
            db.AddRange(app, type);
            added.Add((app, type));
        }

        await db.SaveChangesAsync(cancellationToken);

        db.Add(new CompanyTypeApplicationType { CompanyTypeId = managementType.Id, ApplicationTypeId = management.Id, IsDefault = true });
        result[ApplicationType.ManagementCode] = (management.Id, managementType.Id);
        foreach (var (app, type) in added)
        {
            db.Add(new CompanyTypeApplicationType { CompanyTypeId = type.Id, ApplicationTypeId = app.Id, IsDefault = true });
            db.Add(new ApplicationTypeModule { ApplicationTypeId = app.Id, AppModuleId = admin.Id });
            result[app.Code] = (app.Id, type.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<Dictionary<string, Role>> AddRolesAsync(long tenantId, Dictionary<string, Permission> permissions, CancellationToken cancellationToken)
    {
        var catalogue = IdentitySeeder.Catalogue;
        var roles = new Dictionary<string, Role>
        {
            [IdentitySeeder.AdministratorRole] = await parts.AddRoleAsync(tenantId, IdentitySeeder.AdministratorRole,
                "Everything a tenant may do besides what rank already gives", catalogue.Where(p => p.InAdministrator).Select(p => p.Code), permissions, cancellationToken),
            [ManagerRole] = await parts.AddRoleAsync(tenantId, ManagerRole, "Runs one company: sees people, roles and the audit trail",
                ManagerPermissions, permissions, cancellationToken),
            [IdentitySeeder.ViewerRole] = await parts.AddRoleAsync(tenantId, IdentitySeeder.ViewerRole, "Read-only",
                catalogue.Where(p => p.Viewer).Select(p => p.Code), permissions, cancellationToken),
            [StaffRole] = await parts.AddRoleAsync(tenantId, StaffRole, "Uses the application; no administration", [], permissions, cancellationToken),
        };
        await db.SaveChangesAsync(cancellationToken);
        return roles;
    }

    private async Task<User> AddUserAsync(
        DemoPerson person, long tenantId, (Company Company, DemoCompany Definition, long ApplicationId) home,
        DemoSeedOptions options, CancellationToken cancellationToken)
    {
        var user = await provisioning.NewUserAsync(
            new UserProfile
            {
                UserName = person.UserName, Email = $"{person.UserName.Trim()}@{options.EmailDomain.Trim()}",
                FirstName = person.FirstName, LastName = person.LastName, LanguageCode = options.LanguageCode,
            },
            tenantId, options.Password, options.MustChangePassword, cancellationToken);
        user.EmailConfirmed = true;
        user.ActiveCompanyId = home.Company.Id;
        user.ActiveApplicationTypeId = home.ApplicationId;
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private void Join(User user, long tenantId, long companyId, UserType rank, IEnumerable<Role> roles)
    {
        db.UserCompanies.Add(new UserCompany { IdentityTenantId = tenantId, UserId = user.Id, CompanyId = companyId, MembershipType = rank });
        foreach (var role in roles)
            db.UserCompanyRoles.Add(new UserCompanyRole { IdentityTenantId = tenantId, UserId = user.Id, CompanyId = companyId, RoleId = role.Id });
    }

    private async Task<Dictionary<(string Application, string Name), Role>> AddApplicationRolesAsync(
        long tenantId, DemoTenant tenant, DemoDefinition definition,
        Dictionary<string, (long ApplicationId, long CompanyTypeId)> appIds, Dictionary<string, Permission> permissions, CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, string), Role>();
        foreach (var code in tenant.Companies.Select(c => c.ApplicationCode.Trim().ToUpperInvariant()).Distinct())
        {
            var application = definition.Applications.FirstOrDefault(a => string.Equals(a.Code.Trim(), code, StringComparison.OrdinalIgnoreCase));
            if (application is null)
                continue;

            foreach (var r in application.Roles)
                result[(code, r.Name.Trim().ToUpperInvariant())] = await parts.AddRoleAsync(
                    tenantId, r.Name.Trim(), r.Description ?? string.Empty, r.Permissions.Select(c => c.Trim().ToUpperInvariant()),
                    permissions, cancellationToken, appIds[code].ApplicationId);
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static IEnumerable<Role> RolesOf(
        DemoPerson person, DemoCompany company, Role rankRole, Dictionary<string, Role> shared, Dictionary<(string Application, string Name), Role> byApplication)
    {
        if (person.Roles.Count == 0)
            return [rankRole];

        var application = company.ApplicationCode.Trim().ToUpperInvariant();
        return person.Roles.Select(name =>
            byApplication.TryGetValue((application, name.Trim().ToUpperInvariant()), out var own)
                ? own
                : shared.First(s => string.Equals(s.Key, name.Trim(), StringComparison.OrdinalIgnoreCase)).Value);
    }

    private static DemoAccount Account(User user, Tenant tenant, Company company, DemoCompany definition, string rank)
        => new(user.UserName, $"{user.FirstName} {user.LastName}", tenant.Code, company.Code, definition.ApplicationCode.Trim().ToUpperInvariant(), rank);
}
