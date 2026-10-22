using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class IdentitySeederTests : ApiTestBase
{
    private const string AdminPassword = "Seed-Passw0rd!";

    private async Task<IdentitySeedResult> SeedAsync(IdentitySeedOptions? options = null)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IdentitySeeder>()
            .SeedAsync(options ?? new IdentitySeedOptions { AdminPassword = AdminPassword });
    }

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<HttpClient> AdminClientAsync(string userName = "admin")
    {
        await InDbAsync(async db =>
        {
            var admin = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.UserName == userName);
            admin.ShouldChangePasswordOnNextLogin = false;
            return await db.SaveChangesAsync();
        });
        return Bearer((await SignInAsync(userName, AdminPassword)).GetProperty("accessToken").GetString()!);
    }

    [Fact]
    public async Task Every_permission_the_product_declares_is_classified()
    {
        await Task.CompletedTask;

        var missing = Permissions.All.Except(IdentitySeeder.Catalogue.Select(p => p.Code)).ToList();
        var unknown = IdentitySeeder.Catalogue.Select(p => p.Code).Except(Permissions.All).ToList();

        Assert.Empty(missing);
        Assert.Empty(unknown);
        Assert.Equal(IdentitySeeder.Catalogue.Count, IdentitySeeder.Catalogue.Select(p => p.Code).Distinct().Count());
        Assert.DoesNotContain(IdentitySeeder.Catalogue, p => p.Rank is not null && (p.InAdministrator || p.Viewer));
    }

    [Fact]
    public async Task The_seed_creates_the_first_tenant_company_roles_and_an_administrator_who_must_change_the_password()
    {
        var result = await SeedAsync();

        Assert.Null(result.GeneratedPassword);
        var tenant = await InDbAsync(db => db.Tenants.SingleAsync());
        var company = await InDbAsync(db => db.Companies.IgnoreQueryFilters().SingleAsync());
        Assert.Equal("DEFAULT", tenant.Code);
        Assert.Equal(tenant.Id, company.IdentityTenantId);
        Assert.Equal("MANAGEMENT", await InDbAsync(db => db.CompanyTypes.Where(t => t.Id == company.CompanyTypeId).Select(t => t.Code).SingleAsync()));

        var admin = await InDbAsync(db => db.Users.IgnoreQueryFilters().SingleAsync());
        Assert.True(admin.IsPlatformAdmin);
        Assert.True(admin.ShouldChangePasswordOnNextLogin);
        Assert.Equal(company.Id, admin.ActiveCompanyId);
        Assert.NotEqual(AdminPassword, admin.PasswordHash);
        Assert.Equal(UserType.TenantAdmin, await InDbAsync(db => db.UserCompanies.IgnoreQueryFilters().Select(m => m.MembershipType).SingleAsync()));

        Assert.Equal(Permissions.All.Count, await InDbAsync(db => db.Permissions.CountAsync()));
        Assert.Equal(["Administrator", "Viewer"], await InDbAsync(db => db.Roles.IgnoreQueryFilters().Select(r => r.Name).OrderBy(n => n).ToListAsync()));
        Assert.Equal(["DATE_FORMAT", "TIME_ZONE"], await InDbAsync(db => db.TenantSettings.IgnoreQueryFilters().Select(s => s.Key).OrderBy(k => k).ToListAsync()));
        Assert.Equal(["WEEK_START"], await InDbAsync(db => db.CompanySettings.IgnoreQueryFilters().Select(s => s.Key).ToListAsync()));
    }

    [Fact]
    public async Task Management_permissions_come_with_rank_and_roles_never_carry_them()
    {
        await SeedAsync();

        var management = await InDbAsync(db => db.Permissions.Where(p => p.IsManagement).ToListAsync());
        Assert.All(management, p => Assert.NotNull(p.ManagementRank));
        Assert.Contains(management, p => p.Code == Permissions.UserManage && p.ManagementRank == UserType.CompanyAdmin);
        Assert.Contains(management, p => p.Code == Permissions.RoleManage && p.ManagementRank == UserType.TenantAdmin);

        var inRoles = await InDbAsync(db => db.RolePermissions.IgnoreQueryFilters().Select(rp => rp.PermissionId).Distinct().ToListAsync());
        Assert.Empty(management.Where(p => inRoles.Contains(p.Id)));

        var viewer = await InDbAsync(async db =>
        {
            var roleId = await db.Roles.IgnoreQueryFilters().Where(r => r.Name == "Viewer").Select(r => r.Id).SingleAsync();
            return await (from rp in db.RolePermissions.IgnoreQueryFilters()
                          join p in db.Permissions on rp.PermissionId equals p.Id
                          where rp.RoleId == roleId
                          select p.Code).OrderBy(c => c).ToListAsync();
        });
        Assert.Equal([Permissions.RoleView, Permissions.TenantView, Permissions.UserView], viewer);
    }

    [Fact]
    public async Task The_administrator_can_sign_in_and_work_straight_away()
    {
        var result = await SeedAsync();
        using var admin = await AdminClientAsync();

        var permissions = (await Read(await admin.GetAsync($"/api/User/{result.AdminUserId}/effective-permissions"))).GetProperty("data");
        var codes = permissions.GetProperty("granted").EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToList();
        Assert.Contains(Permissions.UserManage, codes);
        Assert.Contains(Permissions.RoleManage, codes);
        Assert.Contains(Permissions.AuditLogView, codes);

        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync("/api/User", new
        {
            userName = "nurse", email = "nurse@example.test", firstName = "N", lastName = "Urse", password = "An0ther-Passw0rd!",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/AuditLog/paged")).StatusCode);
    }

    [Fact]
    public async Task The_administrator_gets_the_menu_and_the_dashboard_and_a_viewer_gets_less()
    {
        await SeedAsync();
        using var admin = await AdminClientAsync();

        var menu = (await Read(await admin.GetAsync("/api/Navigation/mine"))).GetProperty("data");
        Assert.Equal(["DASHBOARD", "USERS", "ACCESS", "ORGANIZATION", "COMPANIES", "UNITS", "TENANTS", "APPLICATION_TYPES", "SECURITY", "AUDIT", "MENUS", "MENU_STUDIO", "DASHBOARD_MANAGER", "DEFINITIONS", "SETTINGS", "SYSTEM"],
            menu.EnumerateArray().Select(m => m.GetProperty("code").GetString()).ToArray());

        var dashboard = (await Read(await admin.GetAsync("/api/Dashboard/mine"))).GetProperty("data");
        Assert.Equal(["ADMIN_AUDIT_ACTIONS", "ADMIN_KPI_STRIP", "ADMIN_QUICK_ACTIONS", "RECENT_ACTIVITY", "WELCOME"], dashboard.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("widget").GetString()).Order().ToArray());

        var tenantId = await InDbAsync(db => db.Tenants.Select(t => t.Id).SingleAsync());
        var companyId = await InDbAsync(db => db.Companies.IgnoreQueryFilters().Select(c => c.Id).SingleAsync());
        var viewerRole = await InDbAsync(db => db.Roles.IgnoreQueryFilters().Where(r => r.Name == "Viewer").Select(r => r.Id).SingleAsync());
        var vId = await CreateSignInUserAsync("viewer", tenantId, companyId);
        await InDbAsync(async db =>
        {
            db.UserCompanyRoles.Add(new UserCompanyRole { IdentityTenantId = tenantId, UserId = vId, CompanyId = companyId, RoleId = viewerRole });
            var user = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == vId);
            user.ActiveCompanyId = companyId;
            return await db.SaveChangesAsync();
        });
        using var viewer = Bearer((await SignInAsync("viewer")).GetProperty("accessToken").GetString()!);

        var viewerMenu = (await Read(await viewer.GetAsync("/api/Navigation/mine"))).GetProperty("data");
        Assert.Equal(["DASHBOARD", "USERS", "ORGANIZATION"], viewerMenu.EnumerateArray().Select(m => m.GetProperty("code").GetString()).ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/AuditLog/paged")).StatusCode);
    }

    [Fact]
    public async Task Without_a_chosen_password_one_is_generated_and_returned_once_and_it_works()
    {
        var result = await SeedAsync(new IdentitySeedOptions());

        Assert.NotNull(result.GeneratedPassword);
        Assert.True(result.GeneratedPassword!.Length >= 12);
        var response = await Client.PostAsJsonAsync("/api/Auth/login", new { userName = "admin", password = result.GeneratedPassword });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await Read(response)).GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.DoesNotContain(result.GeneratedPassword, await InDbAsync(db => db.Users.IgnoreQueryFilters().Select(u => u.PasswordHash).SingleAsync()));
    }

    [Fact]
    public async Task The_names_come_from_the_options()
    {
        var result = await SeedAsync(new IdentitySeedOptions
        {
            TenantCode = "vendor", TenantName = "Vendor Ltd", CompanyCode = "ist", CompanyName = "Istanbul",
            AdminUserName = "Root", AdminEmail = "Root@Vendor.Test", AdminPassword = AdminPassword, LanguageCode = "TR",
        });

        Assert.Equal("root", result.AdminUserName);
        Assert.Equal("VENDOR", await InDbAsync(db => db.Tenants.Select(t => t.Code).SingleAsync()));
        Assert.Equal("tr", await InDbAsync(db => db.Tenants.Select(t => t.LanguageCode!).SingleAsync()));
        Assert.Equal("IST", await InDbAsync(db => db.Companies.IgnoreQueryFilters().Select(c => c.Code).SingleAsync()));
        Assert.Equal("root@vendor.test", await InDbAsync(db => db.Users.IgnoreQueryFilters().Select(u => u.Email).SingleAsync()));
    }

    [Fact]
    public async Task It_refuses_a_database_that_already_has_users_and_changes_nothing()
    {
        await CreateUserAsync("existing", await CreateTenantAsync());
        var tenants = await InDbAsync(db => db.Tenants.CountAsync());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => SeedAsync());

        Assert.Contains("empty database", ex.Message);
        Assert.Equal(tenants, await InDbAsync(db => db.Tenants.CountAsync()));
        Assert.Equal(0, await InDbAsync(db => db.Permissions.CountAsync()));
    }

    [Fact]
    public async Task A_failed_seed_leaves_nothing_behind()
    {
        await Assert.ThrowsAnyAsync<Exception>(() => SeedAsync(new IdentitySeedOptions { AdminPassword = "x" }));

        Assert.Equal(0, await InDbAsync(db => db.Tenants.CountAsync()));
        Assert.Equal(0, await InDbAsync(db => db.Permissions.CountAsync()));
        Assert.Equal(0, await InDbAsync(db => db.Users.IgnoreQueryFilters().CountAsync()));
    }

    [Fact]
    public async Task The_seed_leaves_no_audit_rows()
    {
        await SeedAsync();

        Assert.Equal(0, await InDbAsync(db => db.AuditLogs.CountAsync()));
    }
}
