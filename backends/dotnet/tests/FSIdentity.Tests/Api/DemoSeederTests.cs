using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class DemoSeederTests : ApiTestBase
{
    private static readonly DemoSeedOptions Options = new();

    private async Task<DemoSeedResult> SeedAsync(DemoDefinition? definition = null, DemoSeedOptions? options = null)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(definition ?? DemoDefinition.Default(), options ?? Options);
    }

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<HttpClient> SignedInAsync(string userName, string? password = null)
        => Bearer((await SignInAsync(userName, password ?? Options.Password)).GetProperty("accessToken").GetString()!);

    private static string[] Codes(JsonElement list) => list.EnumerateArray().Select(m => m.GetProperty("code").GetString()!).ToArray();

    private async Task<string[]> MenuOfAsync(string userName)
    {
        using var client = await SignedInAsync(userName);
        return Codes((await Read(await client.GetAsync("/api/Navigation/mine"))).GetProperty("data"));
    }

    [Fact]
    public async Task The_built_in_demo_has_the_management_tenant_two_customers_and_four_applications()
    {
        var result = await SeedAsync();

        Assert.Equal(["ALPHA", "BETA", "MANAGEMENT"], await InDbAsync(db => db.Tenants.Select(t => t.Code).OrderBy(c => c).ToListAsync()));
        Assert.Equal(["ALPHA-HOSPITALITY", "ALPHA-RETAIL", "BETA-EDUCATION", "BETA-LOGISTICS", "MANAGEMENT"],
            await InDbAsync(db => db.Companies.IgnoreQueryFilters().Select(c => c.Code).OrderBy(c => c).ToListAsync()));
        Assert.Equal(["EDUCATION", "HOSPITALITY", "LOGISTICS", "MANAGEMENT", "RETAIL"],
            await InDbAsync(db => db.ApplicationTypes.Select(a => a.Code).OrderBy(c => c).ToListAsync()));

        Assert.Equal(1 + 2 + 4 + 8, await InDbAsync(db => db.Users.IgnoreQueryFilters().CountAsync()));
        Assert.Equal(Options.Password, result.Password);
        Assert.Contains(result.Accounts, a => a.UserName == "admin" && a.Rank == "Platform administrator" && a.Application == "MANAGEMENT");
        Assert.Equal(2, await InDbAsync(async db =>
        {
            var id = await db.Users.IgnoreQueryFilters().Where(u => u.UserName == "alpha.admin").Select(u => u.Id).SingleAsync();
            return await db.UserCompanies.IgnoreQueryFilters().CountAsync(m => m.UserId == id && m.MembershipType == UserType.TenantAdmin);
        }));
    }

    [Fact]
    public async Task English_is_the_fallback_language_and_the_default_of_everybody()
    {
        await SeedAsync();

        var fallback = await InDbAsync(db => db.Languages.Where(l => l.IsFallback).Select(l => l.Code).ToListAsync());
        Assert.Equal(["en"], fallback);
        Assert.All(await InDbAsync(db => db.Tenants.Select(t => t.LanguageCode).ToListAsync()), l => Assert.Equal("en", l));
        Assert.All(await InDbAsync(db => db.Users.IgnoreQueryFilters().Select(u => u.LanguageCode).ToListAsync()), l => Assert.Equal("en", l));
    }

    [Fact]
    public async Task Each_company_uses_its_own_application_through_its_company_type()
    {
        await SeedAsync();

        var homes = await InDbAsync(async db =>
            await (from c in db.Companies.IgnoreQueryFilters()
                   join t in db.CompanyTypes on c.CompanyTypeId equals t.Id
                   join m in db.CompanyTypeApplicationTypes on t.Id equals m.CompanyTypeId
                   join a in db.ApplicationTypes on m.ApplicationTypeId equals a.Id
                   select new { Company = c.Code, Application = a.Code, m.IsDefault }).ToListAsync());

        Assert.Equal(5, homes.Count);
        Assert.Contains(homes, h => h.Company == "ALPHA-RETAIL" && h.Application == "RETAIL" && h.IsDefault);
        Assert.Contains(homes, h => h.Company == "BETA-EDUCATION" && h.Application == "EDUCATION" && h.IsDefault);
        Assert.Contains(homes, h => h.Company == "MANAGEMENT" && h.Application == "MANAGEMENT");

        using var retail = await SignedInAsync("alpha-retail.user1");
        var me = (await Read(await retail.GetAsync("/api/Auth/me"))).GetProperty("data").GetProperty("user");
        var retailId = await InDbAsync(db => db.ApplicationTypes.Where(a => a.Code == "RETAIL").Select(a => a.Id).SingleAsync());
        Assert.Equal(retailId, me.GetProperty("activeApplicationTypeId").GetInt64());
    }

    [Fact]
    public async Task Ranks_roles_and_the_starting_place_are_set_per_person()
    {
        await SeedAsync();

        var rows = await InDbAsync(async db =>
            await (from u in db.Users.IgnoreQueryFilters()
                   join m in db.UserCompanies.IgnoreQueryFilters() on u.Id equals m.UserId
                   join c in db.Companies.IgnoreQueryFilters() on m.CompanyId equals c.Id
                   select new { u.UserName, m.MembershipType, Company = c.Code, u.ActiveApplicationTypeId, u.ActiveCompanyId, CompanyId = c.Id }).ToListAsync());
        var retailId = await InDbAsync(db => db.ApplicationTypes.Where(a => a.Code == "RETAIL").Select(a => a.Id).SingleAsync());

        Assert.Equal(UserType.CompanyAdmin, rows.Single(r => r.UserName == "alpha-retail.admin").MembershipType);
        Assert.Equal(UserType.Standard, rows.Single(r => r.UserName == "alpha-retail.user2").MembershipType);
        Assert.All(rows.Where(r => r.UserName == "alpha.admin"), r => Assert.Equal(UserType.TenantAdmin, r.MembershipType));
        Assert.Equal(UserType.TenantAdmin, rows.Single(r => r.UserName == "admin").MembershipType);
        var user = rows.Single(r => r.UserName == "alpha-retail.user1");
        Assert.Equal(retailId, user.ActiveApplicationTypeId);
        Assert.Equal(user.CompanyId, user.ActiveCompanyId);

        var roles = await InDbAsync(async db =>
            await (from g in db.UserCompanyRoles.IgnoreQueryFilters()
                   join u in db.Users.IgnoreQueryFilters() on g.UserId equals u.Id
                   join r in db.Roles.IgnoreQueryFilters() on g.RoleId equals r.Id
                   select new { u.UserName, r.Name }).ToListAsync());
        Assert.Equal("Manager", roles.Single(r => r.UserName == "alpha-retail.admin").Name);
        Assert.Equal(["Cashier", "Staff"], roles.Where(r => r.UserName == "alpha-retail.user1").Select(r => r.Name).Order().ToArray());
        Assert.Equal(["Staff"], roles.Where(r => r.UserName == "alpha-retail.user2").Select(r => r.Name).ToArray());
        Assert.All(roles.Where(r => r.UserName == "beta.admin"), r => Assert.Equal("Administrator", r.Name));
    }

    [Fact]
    public async Task A_role_belongs_to_an_application_so_each_application_has_its_own_cashier()
    {
        await SeedAsync();

        var roles = await InDbAsync(async db =>
            await (from r in db.Roles.IgnoreQueryFilters()
                   join t in db.Tenants on r.IdentityTenantId equals t.Id
                   join a in db.ApplicationTypes on r.ApplicationTypeId equals a.Id into apps
                   from a in apps.DefaultIfEmpty()
                   select new { Tenant = t.Code, r.Name, Application = a == null ? null : a.Code }).ToListAsync());

        Assert.Equal(["HOSPITALITY", "RETAIL"], roles.Where(r => r.Tenant == "ALPHA" && r.Name == "Cashier").Select(r => r.Application!).Order().ToArray());
        Assert.DoesNotContain(roles, r => r.Tenant == "ALPHA" && r.Name == "Dispatcher");
        Assert.Contains(roles, r => r.Tenant == "BETA" && r.Name == "Dispatcher" && r.Application == "LOGISTICS");
        Assert.Equal(4, roles.Count(r => r.Tenant == "ALPHA" && r.Application is null));

        var permissions = await InDbAsync(async db =>
            await (from r in db.Roles.IgnoreQueryFilters()
                   join t in db.Tenants on r.IdentityTenantId equals t.Id
                   join a in db.ApplicationTypes on r.ApplicationTypeId equals a.Id
                   join g in db.RolePermissions.IgnoreQueryFilters() on r.Id equals g.RoleId
                   join p in db.Permissions on g.PermissionId equals p.Id
                   where t.Code == "ALPHA" && r.Name == "Cashier"
                   select new { a.Code, Permission = p.Code }).ToListAsync());
        Assert.Equal(["TENANT_VIEW", "USER_VIEW"], permissions.Where(p => p.Code == "RETAIL").Select(p => p.Permission).Order().ToArray());
        Assert.Equal(["TENANT_VIEW"], permissions.Where(p => p.Code == "HOSPITALITY").Select(p => p.Permission).ToArray());
    }

    [Fact]
    public async Task An_application_role_only_counts_while_working_in_that_application()
    {
        await SeedAsync();
        var userId = await InDbAsync(db => db.Users.IgnoreQueryFilters().Where(u => u.UserName == "alpha-retail.user1").Select(u => u.Id).SingleAsync());
        var retailId = await InDbAsync(db => db.ApplicationTypes.Where(a => a.Code == "RETAIL").Select(a => a.Id).SingleAsync());
        var hotelId = await InDbAsync(db => db.ApplicationTypes.Where(a => a.Code == "HOSPITALITY").Select(a => a.Id).SingleAsync());
        var companyId = await InDbAsync(db => db.Users.IgnoreQueryFilters().Where(u => u.Id == userId).Select(u => u.ActiveCompanyId!.Value).SingleAsync());

        using var scope = Factory.Services.CreateScope();
        var resolver = scope.ServiceProvider.GetRequiredService<FSIdentity.Identity.Authorization.PermissionResolver>();

        var inRetail = await resolver.ResolveAsync(userId, companyId, retailId);
        var inHotel = await resolver.ResolveAsync(userId, companyId, hotelId);

        Assert.Contains("USER_VIEW", inRetail.Codes);
        Assert.DoesNotContain("USER_VIEW", inHotel.Codes);
    }

    [Theory]
    [InlineData("Cashier", "MANAGEMENT_THING", "not a permission that a role can hold")]
    [InlineData("Cashier", "USER_MANAGE", "not a permission that a role can hold")]
    public async Task A_role_with_an_unknown_or_management_permission_is_refused_before_anything_is_written(string role, string code, string message)
    {
        var definition = DemoDefinition.Default();
        definition.Applications[0].Roles = [new DemoRole(role, "x", code)];

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SeedAsync(definition));

        Assert.Contains(message, ex.Message);
        Assert.Equal(0, await InDbAsync(db => db.Tenants.CountAsync()));
    }

    [Fact]
    public async Task A_person_given_a_role_nobody_defines_is_refused()
    {
        var definition = DemoDefinition.Default();
        definition.Tenants[0].Companies[0].Users[0].Roles = ["Dispatcher"];

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SeedAsync(definition));

        Assert.Contains("neither the application nor the tenant has", ex.Message);
    }

    [Fact]
    public async Task Everybody_signs_in_at_once_with_the_shared_password()
    {
        var result = await SeedAsync();

        foreach (var account in result.Accounts.DistinctBy(a => a.UserName))
        {
            var response = await Client.PostAsJsonAsync("/api/Auth/login", new { userName = account.UserName, password = Options.Password });
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{account.UserName}: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
            var data = (await Read(response)).GetProperty("data");
            Assert.False(data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean(), account.UserName);
            Assert.Equal(account.UserName, data.GetProperty("user").GetProperty("userName").GetString());
        }
    }

    [Fact]
    public async Task What_each_kind_of_person_sees_follows_their_rank_and_role()
    {
        await SeedAsync();

        Assert.Equal(["DASHBOARD", "USERS", "ACCESS", "ORGANIZATION", "COMPANIES", "UNITS", "TENANTS", "APPLICATION_TYPES", "SECURITY", "AUDIT", "MENUS", "MENU_STUDIO", "DASHBOARD_MANAGER", "DEFINITIONS", "SETTINGS", "SYSTEM"], await MenuOfAsync("admin"));
        Assert.Equal(["DASHBOARD", "USERS", "ACCESS", "ORGANIZATION", "COMPANIES", "UNITS", "SECURITY", "AUDIT", "DASHBOARD_MANAGER", "DEFINITIONS", "SETTINGS"], await MenuOfAsync("alpha.admin"));
        Assert.Equal(["DASHBOARD", "USERS", "ORGANIZATION", "UNITS", "AUDIT", "SETTINGS"], await MenuOfAsync("alpha-retail.admin"));
        Assert.Equal(["DASHBOARD"], await MenuOfAsync("alpha-retail.user2"));
        Assert.Equal(["DASHBOARD", "USERS", "ORGANIZATION"], await MenuOfAsync("alpha-retail.user1"));
        Assert.Equal(["DASHBOARD", "ORGANIZATION"], await MenuOfAsync("alpha-hospitality.user1"));
    }

    [Fact]
    public async Task A_tenant_administrator_belongs_to_every_company_of_the_tenant_and_nobody_crosses_tenants()
    {
        await SeedAsync();

        using var alpha = await SignedInAsync("alpha.admin");
        var user = (await Read(await alpha.GetAsync("/api/Auth/me"))).GetProperty("data").GetProperty("user");
        Assert.Equal(["ALPHA-HOSPITALITY", "ALPHA-RETAIL"], user.GetProperty("companies").EnumerateArray().Select(c => c.GetProperty("code").GetString()!).Order().ToArray());

        var visible = (await Read(await alpha.GetAsync("/api/User"))).GetProperty("data").EnumerateArray().Select(u => u.GetProperty("userName").GetString()!).ToList();
        Assert.Contains("alpha-retail.user1", visible);
        Assert.DoesNotContain("beta.admin", visible);
        Assert.DoesNotContain("admin", visible);
        Assert.Equal(HttpStatusCode.Forbidden, (await alpha.PostAsJsonAsync("/api/Tenant", new { name = "Intruder", code = "INTRUDER" })).StatusCode);
    }

    [Fact]
    public async Task The_password_the_options_name_is_the_one_that_works_and_a_change_can_be_demanded()
    {
        await SeedAsync(options: new DemoSeedOptions { Password = "Another-Demo-1", MustChangePassword = true, EmailDomain = "demo.local" });

        var response = await Client.PostAsync("/api/Auth/login", JsonContent.Create(new { userName = "alpha.admin", password = "Another-Demo-1" }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await Read(response)).GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("alpha.admin@demo.local", await InDbAsync(db => db.Users.IgnoreQueryFilters().Where(u => u.UserName == "alpha.admin").Select(u => u.Email).SingleAsync()));
    }

    [Fact]
    public async Task A_different_demo_is_only_different_data()
    {
        var definition = new DemoDefinition
        {
            Management = DemoDefinition.Default().Management,
            Applications = [new DemoApplication { Code = "clinic", Name = "Clinic" }],
            Tenants =
            [
                new DemoTenant
                {
                    Code = "solo", Name = "Solo", TenantAdmin = new DemoPerson("solo.admin", "Sol", "Oh"),
                    Companies = [new DemoCompany { Code = "SOLO-ONE", Name = "Solo One", ApplicationCode = "CLINIC", Users = [new DemoPerson("solo.nurse", "Nora", "Nurse")] }],
                },
            ],
        };

        var result = await SeedAsync(definition);

        Assert.Equal(["admin", "solo.admin", "solo.nurse"], result.Accounts.Select(a => a.UserName).Distinct().Order().ToArray());
        Assert.Equal(["CLINIC", "MANAGEMENT"], await InDbAsync(db => db.ApplicationTypes.Select(a => a.Code).OrderBy(c => c).ToListAsync()));
        Assert.Equal(["MANAGEMENT", "SOLO"], await InDbAsync(db => db.Tenants.Select(t => t.Code).OrderBy(c => c).ToListAsync()));
    }

    [Fact]
    public async Task The_example_file_is_the_built_in_definition()
    {
        var path = Path.Combine(RepoRoot(), "backends", "dotnet", "demo-seed.example.json");
        var builtIn = JsonSerializer.Serialize(DemoDefinition.Default(), DemoDefinition.Json).Replace("\r\n", "\n") + "\n";

        if (Environment.GetEnvironmentVariable("UPDATE_DEMO_SEED") == "1")
        {
            File.WriteAllText(path, builtIn);
            return;
        }

        Assert.True(File.Exists(path), $"{path} is missing. Create it with UPDATE_DEMO_SEED=1 dotnet test --filter DemoSeederTests");
        Assert.Equal(builtIn, File.ReadAllText(path).Replace("\r\n", "\n"));
        var loaded = DemoDefinition.Load(path);
        loaded.Validate();
        Assert.Equal(DemoDefinition.Default().Tenants.Count, loaded.Tenants.Count);
    }

    [Theory]
    [InlineData("unknown-app")]
    [InlineData("management")]
    public async Task A_company_naming_an_unknown_or_reserved_application_is_refused_before_anything_is_written(string application)
    {
        var definition = DemoDefinition.Default();
        definition.Tenants[0].Companies[0].ApplicationCode = application;

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SeedAsync(definition));

        Assert.Contains("not valid", ex.Message);
        Assert.Equal(0, await InDbAsync(db => db.Tenants.CountAsync()));
    }

    [Fact]
    public async Task Repeated_codes_and_user_names_are_all_reported_at_once()
    {
        var definition = DemoDefinition.Default();
        definition.Tenants[1].Code = "ALPHA";
        definition.Tenants[0].Companies[1].CompanyAdmin = new DemoPerson("ALPHA.ADMIN", "Dup", "Licate");

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => SeedAsync(definition));

        Assert.Contains("Tenant 'ALPHA' is listed twice", ex.Message);
        Assert.Contains("used twice", ex.Message);
    }

    [Fact]
    public async Task It_refuses_a_database_with_users_and_a_failed_run_leaves_nothing()
    {
        await CreateUserAsync("existing", await CreateTenantAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync());
        Assert.Equal(0, await InDbAsync(db => db.Permissions.CountAsync()));
    }

    [Fact]
    public async Task A_password_that_breaks_the_policy_rolls_everything_back()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => SeedAsync(options: new DemoSeedOptions { Password = "x" }));

        Assert.Equal(0, await InDbAsync(db => db.Tenants.CountAsync()));
        Assert.Equal(0, await InDbAsync(db => db.ApplicationTypes.CountAsync()));
        Assert.Equal(0, await InDbAsync(db => db.Users.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task The_demo_leaves_no_audit_rows()
    {
        await SeedAsync();

        Assert.Equal(0, await InDbAsync(db => db.AuditLogs.CountAsync()));
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !(Directory.Exists(Path.Combine(directory.FullName, "contracts")) && Directory.Exists(Path.Combine(directory.FullName, "backends"))))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Could not find the repository root");
    }
}
