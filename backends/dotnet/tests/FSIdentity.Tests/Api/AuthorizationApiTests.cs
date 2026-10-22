using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class AuthorizationApiTests : ApiTestBase
{
    private static object NewCountry(string code = "TR") => new { name = $"{code} land", code, formatCode = "x" };

    private async Task<string> ErrorCodeAsync(HttpResponseMessage response)
        => (await Read(response)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    [Fact]
    public async Task Protected_endpoints_answer_401_to_an_anonymous_caller()
    {
        foreach (var path in new[] { "/api/Country", "/api/Tenant", "/api/Role", "/api/AuditLog/paged", "/api/ModuleScope/mine", "/api/GeographyLookup/countries" })
        {
            var response = await Anonymous.GetAsync(path);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, $"{path} answered {response.StatusCode}");
            Assert.Equal("UNAUTHORIZED", await ErrorCodeAsync(response));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.DeleteAsync("/api/Country/1")).StatusCode);
    }

    [Fact]
    public async Task Only_sign_in_refresh_health_and_the_api_description_are_public()
    {
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.GetAsync("/api/Health")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.GetAsync("/api/Health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName = "x", password = "y" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Anonymous.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = "x" })).StatusCode);
    }

    [Fact]
    public async Task Reading_is_open_to_a_signed_in_user_but_writing_needs_the_resources_permission()
    {
        using var nobody = ClientFor(userId: 1, permissions: "none");
        using var manager = ClientFor(userId: 2, permissions: Permissions.GeographyManage);
        using var wrongOne = ClientFor(userId: 3, permissions: Permissions.RoleManage);

        Assert.Equal(HttpStatusCode.OK, (await nobody.GetAsync("/api/Country")).StatusCode);

        var denied = await nobody.PostAsJsonAsync("/api/Country", NewCountry());
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("PERMISSION_DENIED", await ErrorCodeAsync(denied));
        Assert.Equal(HttpStatusCode.Forbidden, (await wrongOne.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await manager.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
    }

    [Fact]
    public async Task Update_delete_and_restore_are_writes_too()
    {
        var id = await CreateCountryAsync();
        using var nobody = ClientFor(userId: 1, permissions: "none");

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PutAsJsonAsync($"/api/Country/{id}", NewCountry())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.DeleteAsync($"/api/Country/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsync($"/api/Country/{id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Resources_that_guard_their_reads_need_the_view_permission()
    {
        using var nobody = ClientFor(userId: 1, permissions: "none");
        using var viewer = ClientFor(userId: 2, permissions: Permissions.RoleView);

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("/api/Role")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("/api/Tenant")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/Role")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/Role", new { name = "X" })).StatusCode);
    }

    [Fact]
    public async Task The_permission_is_checked_before_the_body_so_a_caller_learns_nothing_from_it()
    {
        using var nobody = ClientFor(userId: 1, permissions: "none");

        var response = await nobody.PostAsJsonAsync("/api/Country", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task The_audit_trail_needs_its_own_permission()
    {
        using var nobody = ClientFor(userId: 1, permissions: "none");
        using var auditor = ClientFor(userId: 2, permissions: Permissions.AuditLogView);

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("/api/AuditLog/paged")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/AuditLog/paged")).StatusCode);
    }

    [Fact]
    public void Every_permission_code_is_listed_for_seeding_and_is_unique()
    {
        Assert.Contains(Permissions.UserManage, Permissions.All);
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct().Count());
        Assert.All(Permissions.All, code => Assert.Equal(code.ToUpperInvariant(), code));
    }

    private async Task<(long UserId, long CompanyId, long TenantId)> MemberAsync(string userName = "ada")
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);
        var userId = await CreateSignInUserAsync(userName, tenantId, companyId);
        return (userId, companyId, tenantId);
    }

    private async Task<long> RoleWithAsync(string roleName, params string[] permissionCodes)
    {
        var roleId = await CreateRoleAsync(roleName);
        foreach (var code in permissionCodes)
        {
            var permissionId = await CreatePermissionAsync(code);
            await PostIdAsync("/api/RolePermission", new { roleId, permissionId });
        }

        return roleId;
    }

    private Task GiveRoleAsync(long userId, long companyId, long roleId)
        => PostIdAsync("/api/UserCompanyRole", new { userId, companyId, roleId });

    private async Task<HttpClient> SignedInAsync(string userName = "ada")
        => Bearer((await SignInAsync(userName)).GetProperty("accessToken").GetString()!);

    [Fact]
    public async Task A_user_may_do_what_their_roles_in_the_active_company_allow()
    {
        var (userId, companyId, _) = await MemberAsync();
        await GiveRoleAsync(userId, companyId, await RoleWithAsync("Geographer", Permissions.GeographyManage));
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.Created, (await user.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Language", new { code = "xx", name = "X" })).StatusCode);
    }

    [Fact]
    public async Task A_user_with_no_role_may_read_but_not_write()
    {
        await MemberAsync();
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/Country")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
    }

    [Fact]
    public async Task A_change_to_the_roles_takes_effect_on_the_next_request_without_signing_in_again()
    {
        var (userId, companyId, _) = await MemberAsync();
        using var user = await SignedInAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Country", NewCountry("AA"))).StatusCode);

        var grant = await PostIdAsync("/api/UserCompanyRole",
            new { userId, companyId, roleId = await RoleWithAsync("Geographer", Permissions.GeographyManage) });
        Assert.Equal(HttpStatusCode.Created, (await user.PostAsJsonAsync("/api/Country", NewCountry("BB"))).StatusCode);

        await Client.DeleteAsync($"/api/UserCompanyRole/{grant}");
        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Country", NewCountry("CC"))).StatusCode);
    }

    [Fact]
    public async Task A_permission_taken_from_a_user_beats_the_role_and_one_granted_adds_to_it()
    {
        var (userId, companyId, _) = await MemberAsync();
        var geography = await CreatePermissionAsync(Permissions.GeographyManage);
        var language = await CreatePermissionAsync(Permissions.LanguageManage);
        var roleId = await CreateRoleAsync("Geographer");
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId = geography });
        await GiveRoleAsync(userId, companyId, roleId);
        await PostIdAsync("/api/UserCompanyPermission", new { userId, companyId, permissionId = geography, isGranted = false });
        await PostIdAsync("/api/UserCompanyPermission", new { userId, companyId, permissionId = language, isGranted = true });
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await user.PostAsJsonAsync("/api/Language", new { code = "xx", name = "X" })).StatusCode);
    }

    [Fact]
    public async Task A_role_made_for_another_application_gives_nothing_here()
    {
        var (userId, companyId, _) = await MemberAsync();
        var typeId = await PostIdAsync("/api/CompanyType", new { name = "Clinic", code = "CLINIC" });
        await Client.PutAsJsonAsync($"/api/Company/{companyId}", new { name = "HQ", code = "HQ", companyTypeId = typeId });
        var home = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        var other = await PostIdAsync("/api/ApplicationType", new { name = "Lab", code = "LAB" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = home, isDefault = true });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = other });
        var permissionId = await CreatePermissionAsync(Permissions.GeographyManage);
        var forLab = await PostIdAsync("/api/Role", new { name = "Lab geographer", applicationTypeId = other });
        await PostIdAsync("/api/RolePermission", new { roleId = forLab, permissionId });
        await GiveRoleAsync(userId, companyId, forLab);

        using var inHome = await SignedInAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await inHome.PostAsJsonAsync("/api/Country", NewCountry("AA"))).StatusCode);

        var switched = await inHome.PostAsJsonAsync("/api/Auth/change-application", new { applicationTypeId = other });
        using var inLab = Bearer((await Read(switched)).GetProperty("data").GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.Created, (await inLab.PostAsJsonAsync("/api/Country", NewCountry("BB"))).StatusCode);
    }

    [Fact]
    public async Task A_user_who_is_not_a_member_of_the_active_company_has_no_permissions()
    {
        var (userId, companyId, _) = await MemberAsync();
        await GiveRoleAsync(userId, companyId, await RoleWithAsync("Geographer", Permissions.GeographyManage));
        using var user = await SignedInAsync();
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.UserCompanies.Single().IsActive = false;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);
    }

    [Fact]
    public async Task An_inactive_role_or_permission_gives_nothing()
    {
        var (userId, companyId, _) = await MemberAsync();
        var roleId = await RoleWithAsync("Geographer", Permissions.GeographyManage);
        await GiveRoleAsync(userId, companyId, roleId);
        using var user = await SignedInAsync();
        Assert.Equal(HttpStatusCode.Created, (await user.PostAsJsonAsync("/api/Country", NewCountry("AA"))).StatusCode);

        await Client.PutAsJsonAsync($"/api/Role/{roleId}", new { name = "Geographer", isActive = false });

        Assert.Equal(HttpStatusCode.Forbidden, (await user.PostAsJsonAsync("/api/Country", NewCountry("BB"))).StatusCode);
    }

    [Fact]
    public async Task A_platform_administrator_may_do_everything_and_me_lists_what_a_user_may_do()
    {
        await CreateSignInUserAsync("root", platformAdmin: true);
        using var root = await SignedInAsync("root");
        Assert.Equal(HttpStatusCode.Created, (await root.PostAsJsonAsync("/api/Country", NewCountry())).StatusCode);

        var (userId, companyId, _) = await MemberAsync("grace");
        await GiveRoleAsync(userId, companyId, await RoleWithAsync("Geographer", Permissions.GeographyManage));
        using var grace = await SignedInAsync("grace");
        var me = (await Read(await grace.GetAsync("/api/Auth/me"))).GetProperty("data");

        Assert.Equal([Permissions.GeographyManage], me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
        var rootMe = (await Read(await root.GetAsync("/api/Auth/me"))).GetProperty("data");
        Assert.Equal(Permissions.All.Count, rootMe.GetProperty("permissions").GetArrayLength());
    }

    [Fact]
    public async Task While_the_password_must_be_changed_only_the_account_endpoints_work()
    {
        await CreateSignInUserAsync("ada");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single().ShouldChangePasswordOnNextLogin = true;
            await db.SaveChangesAsync();
        }
        using var user = await SignedInAsync();

        var blocked = await user.GetAsync("/api/Country");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", await ErrorCodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/Auth/me")).StatusCode);

        var changed = await user.PostAsJsonAsync("/api/Auth/change-password",
            new { currentPassword = Password, newPassword = "Brand-new-1" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        using var fresh = Bearer((await SignInAsync("ada", "Brand-new-1")).GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await fresh.GetAsync("/api/Country")).StatusCode);
    }
}
