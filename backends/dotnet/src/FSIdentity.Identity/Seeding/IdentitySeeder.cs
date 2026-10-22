using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Menus;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Entities.Workspace;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Users;
using FSIdentity.Identity.Workspace;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Seeding;

public class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    [Required, StringLength(32)]
    public string TenantCode { get; set; } = "DEFAULT";

    [Required, StringLength(256)]
    public string TenantName { get; set; } = "Default tenant";

    [Required, StringLength(32)]
    public string CompanyCode { get; set; } = "HQ";

    [Required, StringLength(256)]
    public string CompanyName { get; set; } = "Head office";

    [Required, StringLength(256)]
    public string AdminUserName { get; set; } = "admin";

    [Required, EmailAddress, StringLength(256)]
    public string AdminEmail { get; set; } = "admin@example.test";

    public string? AdminPassword { get; set; }

    [Required, StringLength(8)]
    public string LanguageCode { get; set; } = "en";
}

public sealed record IdentitySeedResult(long TenantId, long CompanyId, long AdminUserId, string AdminUserName, string? GeneratedPassword);

public class IdentitySeeder(AppDbContext db, IUserProvisioning provisioning, ApplicationSeeder applications)
{
    public const string AdministratorRole = "Administrator";
    public const string ViewerRole = "Viewer";

    public const string CompanyTypeCode = ApplicationType.ManagementCode;

    public const string MainMenu = "MAIN";

    public static readonly IReadOnlyList<PermissionSeed> Catalogue =
    [
        new(Permissions.UserManage, "Manage users", Rank: UserType.CompanyAdmin),
        new(Permissions.SessionManage, "Manage sessions", Rank: UserType.CompanyAdmin),
        new(Permissions.RoleManage, "Manage roles", Rank: UserType.TenantAdmin),
        new(Permissions.SecurityManage, "Manage security policies", Rank: UserType.TenantAdmin),
        new(Permissions.Impersonate, "Act as another user", Rank: UserType.TenantAdmin),

        new(Permissions.TenantView, "View the tenant", InAdministrator: true, Viewer: true),
        new(Permissions.UserView, "View users", InAdministrator: true, Viewer: true),
        new(Permissions.RoleView, "View roles", InAdministrator: true, Viewer: true),
        new(Permissions.AuditLogView, "View the audit trail", InAdministrator: true),
        new(Permissions.CompanyManage, "Manage companies", InAdministrator: true),
        new(Permissions.MenuManage, "Adjust the menu", InAdministrator: true),
        new(Permissions.DashboardManage, "Manage dashboards", InAdministrator: true),
        new(Permissions.UnitManage, "Manage organisation units", InAdministrator: true),
        new(Permissions.SettingsManage, "Manage settings", InAdministrator: true),

        new(Permissions.GeographyManage, "Manage geography"),
        new(Permissions.LanguageManage, "Manage languages"),
        new(Permissions.TenantManage, "Manage tenants"),
        new(Permissions.CompanyTypeManage, "Manage company types"),
        new(Permissions.ApplicationTypeManage, "Manage application types"),
        new(Permissions.PermissionManage, "Manage permissions"),
        new(Permissions.MenuDesign, "Design the menu"),
    ];

    public async Task<IdentitySeedResult> SeedAsync(IdentitySeedOptions options, CancellationToken cancellationToken = default)
    {
        if (await db.Users.IgnoreQueryFilters().AnyAsync(cancellationToken))
            throw new InvalidOperationException("The database already has users. The identity seed is for an empty database only.");

        using var _ = AuditLogSuppression.Suppress();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        await applications.SeedAsync(cancellationToken);
        var management = await db.ApplicationTypes.SingleAsync(a => a.Code == ApplicationType.ManagementCode, cancellationToken);

        var companyType = new CompanyType { Code = CompanyTypeCode, Name = "Management", Description = "The vendor's own company" };
        var tenant = new Tenant { Code = options.TenantCode.Trim().ToUpperInvariant(), Name = options.TenantName.Trim(), LanguageCode = options.LanguageCode.Trim().ToLowerInvariant() };
        db.AddRange(companyType, tenant);
        await db.SaveChangesAsync(cancellationToken);

        db.Add(new CompanyTypeApplicationType { CompanyTypeId = companyType.Id, ApplicationTypeId = management.Id, IsDefault = true });
        AddTenantSettings(tenant.Id);

        var company = new Company
        {
            IdentityTenantId = tenant.Id,
            Code = options.CompanyCode.Trim().ToUpperInvariant(),
            Name = options.CompanyName.Trim(),
            CompanyTypeId = companyType.Id,
        };
        db.Add(company);
        await db.SaveChangesAsync(cancellationToken);

        AddCompanySettings(tenant.Id, company.Id);

        var permissions = await AddPermissionsAsync(cancellationToken);

        var administrator = await AddRoleAsync(tenant.Id, AdministratorRole, "Everything a tenant may do besides what rank already gives",
            Catalogue.Where(p => p.InAdministrator).Select(p => p.Code), permissions, cancellationToken);
        await AddRoleAsync(tenant.Id, ViewerRole, "Read-only", Catalogue.Where(p => p.Viewer).Select(p => p.Code), permissions, cancellationToken);

        var generated = string.IsNullOrWhiteSpace(options.AdminPassword) ? provisioning.GenerateTemporaryPassword() : null;
        var admin = await provisioning.NewUserAsync(
            new UserProfile
            {
                UserName = options.AdminUserName, Email = options.AdminEmail, FirstName = "System", LastName = "Administrator",
                LanguageCode = options.LanguageCode,
            },
            tenant.Id, generated ?? options.AdminPassword!, mustChangePassword: true, cancellationToken);
        admin.IsPlatformAdmin = true;
        admin.EmailConfirmed = true;
        db.Users.Add(admin);
        await db.SaveChangesAsync(cancellationToken);

        db.UserCompanies.Add(new UserCompany { IdentityTenantId = tenant.Id, UserId = admin.Id, CompanyId = company.Id, MembershipType = UserType.TenantAdmin });
        db.UserCompanyRoles.Add(new UserCompanyRole { IdentityTenantId = tenant.Id, UserId = admin.Id, CompanyId = company.Id, RoleId = administrator.Id });
        admin.ActiveCompanyId = company.Id;
        admin.ActiveApplicationTypeId = management.Id;

        await AddMenuAsync(management.Id, permissions, cancellationToken);
        await AddDashboardAsync(permissions, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return new IdentitySeedResult(tenant.Id, company.Id, admin.Id, admin.UserName, generated);
    }

    public async Task<Dictionary<string, Permission>> AddPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissions = Catalogue.ToDictionary(
            p => p.Code,
            p => new Permission { Code = p.Code, Name = p.Name, IsManagement = p.Rank is not null, ManagementRank = p.Rank });
        db.Permissions.AddRange(permissions.Values);
        await db.SaveChangesAsync(cancellationToken);
        return permissions;
    }

    public async Task<Role> AddRoleAsync(
        long tenantId, string name, string description, IEnumerable<string> permissionCodes,
        Dictionary<string, Permission> permissions, CancellationToken cancellationToken, long? applicationTypeId = null)
    {
        var role = new Role { IdentityTenantId = tenantId, Name = name, Description = description, ApplicationTypeId = applicationTypeId };
        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var code in permissionCodes.Distinct())
            db.RolePermissions.Add(new RolePermission { IdentityTenantId = tenantId, RoleId = role.Id, PermissionId = permissions[code].Id });
        return role;
    }

    public void AddTenantSettings(long tenantId)
    {
        foreach (var (key, value) in TenantBaselineHook.Defaults)
            db.Add(new TenantSetting { IdentityTenantId = tenantId, Key = key, Value = value });
    }

    public void AddCompanySettings(long tenantId, long companyId)
    {
        foreach (var (key, value) in CompanyBaselineHook.Defaults)
            db.Add(new CompanySetting { IdentityTenantId = tenantId, IdentityCompanyId = companyId, Key = key, Value = value });
    }

    public async Task AddMenuAsync(long? applicationId, Dictionary<string, Permission> permissions, CancellationToken cancellationToken)
    {
        var type = new MenuType { Code = MainMenu, Name = "Main menu" };
        db.MenuTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);

        var entries = new (string Code, string En, string Tr, string? Path, string Icon, string? Permission, string Module, string? Parent)[]
        {
            ("DASHBOARD", "Dashboard", "Panel", "/dashboard", "home", null, AppModuleCodes.Core, null),
            ("USERS", "Users", "Kullanıcılar", "/admin/users", "users", Permissions.UserView, AppModuleCodes.Admin, null),
            ("ACCESS", "Authorisation Studio", "Yetkilendirme Stüdyosu", "/admin/access", "key", Permissions.RoleManage, AppModuleCodes.Admin, null),
            ("ORGANIZATION", "Organisation", "Organizasyon", "/admin/organization", "tree", Permissions.TenantView, AppModuleCodes.Admin, null),
            ("COMPANIES", "Companies", "Şirketler", "/admin/companies", "building", Permissions.CompanyManage, AppModuleCodes.Admin, null),
            ("UNITS", "Units", "Birimler", "/admin/units", "tree", Permissions.UnitManage, AppModuleCodes.Admin, null),
            ("TENANTS", "Tenants", "Kiracılar", "/admin/tenants", "globe", Permissions.TenantManage, AppModuleCodes.Admin, null),
            ("APPLICATION_TYPES", "Application types", "Uygulama türleri", "/admin/application-types", "shield", Permissions.ApplicationTypeManage, AppModuleCodes.Admin, null),
            ("SECURITY", "Security", "Güvenlik", "/admin/security", "lock", Permissions.SecurityManage, AppModuleCodes.Admin, null),
            ("AUDIT", "Audit trail", "Denetim kayıtları", "/admin/audit", "history", Permissions.AuditLogView, AppModuleCodes.Admin, null),
            ("MENUS", "Menu management", "Menü yönetimi", "/admin/menus", "settings", Permissions.MenuDesign, AppModuleCodes.Admin, null),
            ("MENU_STUDIO", "Menu Studio", "Menü Stüdyosu", "/admin/menu-studio", "settings", Permissions.MenuDesign, AppModuleCodes.Admin, null),
            ("DASHBOARD_MANAGER", "Dashboard Manager", "Pano Yöneticisi", "/admin/dashboard-manager", "home", Permissions.DashboardManage, AppModuleCodes.Admin, null),
            ("DEFINITIONS", "Definitions", "Tanımlar", null, "settings", null, AppModuleCodes.Admin, null),
            ("DEF_COMPANY_TYPES", "Company types", "Şirket türleri", "/admin/def/company-types", "building", Permissions.CompanyTypeManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_COUNTRIES", "Countries", "Ülkeler", "/admin/def/countries", "globe", Permissions.GeographyManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_STATES", "Regions", "Bölgeler", "/admin/def/states", "globe", Permissions.GeographyManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_CITIES", "Cities", "Şehirler", "/admin/def/cities", "globe", Permissions.GeographyManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_LANGUAGES", "Languages", "Diller", "/admin/def/languages", "globe", Permissions.LanguageManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_COMPONENT_TYPES", "Component types", "Bileşen türleri", "/admin/def/component-types", "settings", Permissions.DashboardManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_MENU_TYPES", "Menu types", "Menü türleri", "/admin/def/menu-types", "settings", Permissions.MenuDesign, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_MENU_TOGGLES", "Menu feature switches", "Menü özellik anahtarları", "/admin/def/menu-toggles", "settings", Permissions.MenuManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("DEF_MENU_OVERRIDES", "Menu permission overrides", "Menü izin istisnaları", "/admin/def/menu-overrides", "settings", Permissions.MenuManage, AppModuleCodes.Admin, "DEFINITIONS"),
            ("SETTINGS", "Settings", "Ayarlar", "/settings", "settings", Permissions.SettingsManage, AppModuleCodes.Admin, null),
            ("SYSTEM", "System", "Sistem", "/admin/system", "settings", Permissions.TenantManage, AppModuleCodes.Admin, null),
        };

        var order = 0;
        var saved = new Dictionary<string, Menu>();
        foreach (var e in entries)
        {
            var menu = new Menu
            {
                Code = e.Code, Title = e.En, Path = e.Path, Icon = e.Icon, DisplayOrder = order += 10,
                MenuTypeId = type.Id, ApplicationTypeId = applicationId,
                ParentId = e.Parent is null ? null : saved[e.Parent].Id,
                PermissionId = e.Permission is null ? null : permissions[e.Permission].Id,
                ModuleCode = e.Module,
            };
            db.Menus.Add(menu);
            await db.SaveChangesAsync(cancellationToken);
            saved[e.Code] = menu;
            db.MenuLocalizations.Add(new MenuLocalization { MenuId = menu.Id, LanguageCode = "tr", Title = e.Tr });
        }
    }

    public async Task AddDashboardAsync(Dictionary<string, Permission> permissions, CancellationToken cancellationToken)
    {
        var card = new ComponentType { Code = "CARD", Name = "Card", Description = "A titled block of content" };
        db.ComponentTypes.Add(card);
        await db.SaveChangesAsync(cancellationToken);

        db.DashboardWidgets.AddRange(
            new DashboardWidget { Code = "WELCOME", Title = "Welcome", ComponentTypeId = card.Id, ModuleCode = AppModuleCodes.Core, DefaultWidth = 12, DefaultHeight = 3 },
            new DashboardWidget { Code = "ADMIN_QUICK_ACTIONS", Title = "Quick actions", ComponentTypeId = card.Id, ModuleCode = AppModuleCodes.Admin, DefaultWidth = 12, DefaultHeight = 3 },
            new DashboardWidget { Code = "ADMIN_KPI_STRIP", Title = "Overview", ComponentTypeId = card.Id, ModuleCode = AppModuleCodes.Admin, DefaultWidth = 24, DefaultHeight = 3 },
            new DashboardWidget
            {
                Code = "ADMIN_AUDIT_ACTIONS", Title = "Changes by action", ComponentTypeId = card.Id, ModuleCode = AppModuleCodes.Admin,
                PermissionId = permissions[Permissions.AuditLogView].Id, DefaultWidth = 12, DefaultHeight = 6,
            },
            new DashboardWidget
            {
                Code = "RECENT_ACTIVITY", Title = "Recent activity", ComponentTypeId = card.Id, ModuleCode = AppModuleCodes.Admin,
                PermissionId = permissions[Permissions.AuditLogView].Id, DefaultWidth = 12, DefaultHeight = 6,
            });
    }
}

public sealed record PermissionSeed(string Code, string Name, UserType? Rank = null, bool InAdministrator = false, bool Viewer = false);
