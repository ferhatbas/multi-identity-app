using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Accounts;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class GuardApiTests : ApiTestBase
{
    private long _tenantId;
    private long _companyId;

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<string> ErrorCodeAsync(HttpResponseMessage response)
        => (await Read(response)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private async Task SetUpAsync()
    {
        _tenantId = await CreateTenantAsync();
        _companyId = await CreateCompanyAsync(_tenantId);
        foreach (var (code, rank) in new[]
                 {
                     (Permissions.UserView, 2), (Permissions.UserManage, 2), (Permissions.RoleView, 2),
                     (Permissions.RoleManage, 2), (Permissions.SessionManage, 2), (Permissions.Impersonate, 3),
                 })
            await PostIdAsync("/api/Permission", new { name = code, code, isManagement = true, managementRank = rank });
    }

    private async Task<long> MemberAsync(string userName, UserType type)
        => await CreateSignInUserAsync(userName, _tenantId, _companyId, membership: type);

    private async Task<HttpClient> AsAsync(string userName)
        => Bearer((await SignInAsync(userName)).GetProperty("accessToken").GetString()!);

    [Fact]
    public async Task A_management_permission_must_name_a_rank_and_only_it_may()
    {
        var noRank = await Client.PostAsJsonAsync("/api/Permission", new { name = "X", code = "X1", isManagement = true });
        var platformRank = await Client.PostAsJsonAsync("/api/Permission", new { name = "X", code = "X2", isManagement = true, managementRank = 4 });
        var strayRank = await Client.PostAsJsonAsync("/api/Permission", new { name = "X", code = "X3", managementRank = 2 });
        var good = await Client.PostAsJsonAsync("/api/Permission", new { name = "X", code = "X4", isManagement = true, managementRank = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, noRank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, platformRank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, strayRank.StatusCode);
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        Assert.Equal(3, (await Read(good)).GetProperty("data").GetProperty("managementRank").GetInt32());
    }

    [Fact]
    public async Task A_management_permission_cannot_be_given_through_a_role_or_a_personal_grant()
    {
        await SetUpAsync();
        var userId = await MemberAsync("ada", UserType.Standard);
        var managing = await InDbAsync(db => db.Permissions.Where(p => p.Code == Permissions.UserManage).Select(p => p.Id).SingleAsync());
        var roleId = await CreateRoleAsync("Helper");

        var viaRole = await Client.PostAsJsonAsync("/api/RolePermission", new { roleId, permissionId = managing });
        var viaGrant = await Client.PostAsJsonAsync("/api/UserCompanyPermission",
            new { userId, companyId = _companyId, permissionId = managing, isGranted = true });

        Assert.Equal(HttpStatusCode.Forbidden, viaRole.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viaGrant.StatusCode);
    }

    [Fact]
    public async Task Even_a_row_that_slipped_in_gives_nothing_for_a_management_permission()
    {
        await SetUpAsync();
        var userId = await MemberAsync("ada", UserType.Standard);
        var managing = await InDbAsync(db => db.Permissions.Where(p => p.Code == Permissions.UserManage).Select(p => p.Id).SingleAsync());
        var roleId = await CreateRoleAsync("Helper");
        await InDbAsync(async db =>
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = managing });
            db.UserCompanyRoles.Add(new UserCompanyRole { UserId = userId, CompanyId = _companyId, RoleId = roleId });
            return await db.SaveChangesAsync();
        });
        using var ada = await AsAsync("ada");

        var me = (await Read(await ada.GetAsync("/api/Auth/me"))).GetProperty("data");

        Assert.DoesNotContain(me.GetProperty("permissions").EnumerateArray(), p => p.GetString() == Permissions.UserManage);
        Assert.Equal(HttpStatusCode.Forbidden, (await ada.PostAsJsonAsync("/api/Role", new { name = "Z" })).StatusCode);
    }

    [Fact]
    public async Task Management_permissions_come_with_the_membership_rank()
    {
        await SetUpAsync();
        await MemberAsync("reader", UserType.Reader);
        await MemberAsync("staff", UserType.Standard);
        await MemberAsync("boss", UserType.CompanyAdmin);
        await MemberAsync("chief", UserType.TenantAdmin);

        async Task<string[]> PermissionsOf(string name)
        {
            using var client = await AsAsync(name);
            var me = (await Read(await client.GetAsync("/api/Auth/me"))).GetProperty("data");
            return me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).Order().ToArray();
        }

        Assert.Empty(await PermissionsOf("reader"));
        Assert.Empty(await PermissionsOf("staff"));
        Assert.Equal([Permissions.RoleManage, Permissions.RoleView, Permissions.SessionManage, Permissions.UserManage, Permissions.UserView],
            await PermissionsOf("boss"));
        Assert.Contains(Permissions.Impersonate, await PermissionsOf("chief"));
        Assert.Equal(6, (await PermissionsOf("chief")).Length);
    }

    [Fact]
    public async Task The_effective_permissions_name_the_rank_as_their_source()
    {
        await SetUpAsync();
        var bossId = await MemberAsync("boss", UserType.CompanyAdmin);

        var data = (await Read(await Client.GetAsync($"/api/User/{bossId}/effective-permissions?companyId={_companyId}"))).GetProperty("data");

        var userManage = data.GetProperty("granted").EnumerateArray().Single(p => p.GetProperty("code").GetString() == Permissions.UserManage);
        Assert.Equal(["rank:CompanyAdmin"], userManage.GetProperty("via").EnumerateArray().Select(v => v.GetString()));
    }

    [Fact]
    public async Task A_company_administrator_manages_ordinary_people_but_not_another_administrator()
    {
        await SetUpAsync();
        var staff = await MemberAsync("staff", UserType.Standard);
        var peer = await MemberAsync("peer", UserType.CompanyAdmin);
        await MemberAsync("boss", UserType.CompanyAdmin);
        using var boss = await AsAsync("boss");

        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsync($"/api/User/{staff}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PostAsync($"/api/User/{peer}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.DeleteAsync($"/api/User/{peer}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PutAsJsonAsync($"/api/User/{peer}", new
        {
            userName = "peer", email = "peer@example.test", firstName = "P", lastName = "P",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PostAsJsonAsync($"/api/User/{peer}/copy", new { userName = "twin", email = "twin@example.test" })).StatusCode);
    }

    [Fact]
    public async Task A_tenant_administrator_manages_company_administrators_but_not_their_own_rank()
    {
        await SetUpAsync();
        var companyAdmin = await MemberAsync("boss", UserType.CompanyAdmin);
        var otherChief = await MemberAsync("chief2", UserType.TenantAdmin);
        await MemberAsync("chief", UserType.TenantAdmin);
        using var chief = await AsAsync("chief");

        Assert.Equal(HttpStatusCode.OK, (await chief.PostAsync($"/api/User/{companyAdmin}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await chief.PostAsync($"/api/User/{otherChief}/reset-password", null)).StatusCode);
    }

    [Fact]
    public async Task Nobody_gives_a_rank_at_or_above_their_own()
    {
        await SetUpAsync();
        var newcomer = await CreateUserAsync("newcomer", _tenantId);
        await MemberAsync("boss", UserType.CompanyAdmin);
        await MemberAsync("chief", UserType.TenantAdmin);
        using var boss = await AsAsync("boss");
        using var chief = await AsAsync("chief");

        var tooHigh = await boss.PostAsJsonAsync("/api/UserCompany", new { userId = newcomer, companyId = _companyId, membershipType = 2 });
        var fine = await boss.PostAsJsonAsync("/api/UserCompany", new { userId = newcomer, companyId = _companyId, membershipType = 1 });
        var chiefGives = await chief.PostAsJsonAsync("/api/UserCompany",
            new { userId = await CreateUserAsync("second", _tenantId), companyId = _companyId, membershipType = 2 });
        var chiefTooHigh = await chief.PostAsJsonAsync("/api/UserCompany",
            new { userId = await CreateUserAsync("third", _tenantId), companyId = _companyId, membershipType = 3 });

        Assert.Equal(HttpStatusCode.Forbidden, tooHigh.StatusCode);
        Assert.Equal(HttpStatusCode.Created, fine.StatusCode);
        Assert.Equal(HttpStatusCode.Created, chiefGives.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, chiefTooHigh.StatusCode);
    }

    [Fact]
    public async Task Platform_administrator_is_never_a_membership_even_for_a_platform_administrator()
    {
        await SetUpAsync();
        var newcomer = await CreateUserAsync("newcomer", _tenantId);

        var response = await Client.PostAsJsonAsync("/api/UserCompany",
            new { userId = newcomer, companyId = _companyId, membershipType = 4 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Promoting_demoting_or_removing_a_higher_ranked_member_is_refused()
    {
        await SetUpAsync();
        var peer = await MemberAsync("peer", UserType.CompanyAdmin);
        var staff = await MemberAsync("staff", UserType.Standard);
        await MemberAsync("boss", UserType.CompanyAdmin);
        var peerMembership = await InDbAsync(db => db.UserCompanies.Where(m => m.UserId == peer).Select(m => m.Id).SingleAsync());
        var staffMembership = await InDbAsync(db => db.UserCompanies.Where(m => m.UserId == staff).Select(m => m.Id).SingleAsync());
        using var boss = await AsAsync("boss");

        Assert.Equal(HttpStatusCode.Forbidden, (await boss.DeleteAsync($"/api/UserCompany/{peerMembership}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PutAsJsonAsync($"/api/UserCompany/{peerMembership}",
            new { userId = peer, companyId = _companyId, membershipType = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PutAsJsonAsync($"/api/UserCompany/{staffMembership}",
            new { userId = staff, companyId = _companyId, membershipType = 2 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await boss.PutAsJsonAsync($"/api/UserCompany/{staffMembership}",
            new { userId = staff, companyId = _companyId, membershipType = 0 })).StatusCode);
    }

    [Fact]
    public async Task Roles_and_personal_grants_cannot_be_given_to_somebody_who_outranks_you()
    {
        await SetUpAsync();
        var peer = await MemberAsync("peer", UserType.CompanyAdmin);
        var staff = await MemberAsync("staff", UserType.Standard);
        await MemberAsync("boss", UserType.CompanyAdmin);
        var roleId = await InDbAsync(async db =>
        {
            var role = new Role { Name = "Helper", IdentityTenantId = _tenantId };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
            return role.Id;
        });
        var permissionId = await CreatePermissionAsync("SOME_PERMISSION");
        using var boss = await AsAsync("boss");

        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PostAsJsonAsync("/api/UserCompanyRole",
            new { userId = peer, companyId = _companyId, roleId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PostAsJsonAsync("/api/UserCompanyPermission",
            new { userId = peer, companyId = _companyId, permissionId })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await boss.PostAsJsonAsync("/api/UserCompanyRole",
            new { userId = staff, companyId = _companyId, roleId })).StatusCode);
    }

    [Fact]
    public async Task People_may_edit_their_own_profile_but_not_delete_themselves()
    {
        await SetUpAsync();
        var bossId = await MemberAsync("boss", UserType.CompanyAdmin);
        using var boss = await AsAsync("boss");

        var edit = await boss.PutAsJsonAsync($"/api/User/{bossId}", new
        {
            userName = "boss", email = "boss@example.test", firstName = "Better", lastName = "Boss",
        });
        var delete = await boss.DeleteAsync($"/api/User/{bossId}");

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    [Fact]
    public async Task A_persons_rank_is_the_highest_they_hold_anywhere()
    {
        await SetUpAsync();
        var otherCompany = await CreateCompanyAsync(_tenantId, "BRANCH");
        var shared = await MemberAsync("shared", UserType.Standard);
        await InDbAsync(async db =>
        {
            db.UserCompanies.Add(new UserCompany { UserId = shared, CompanyId = otherCompany, MembershipType = UserType.CompanyAdmin, IdentityTenantId = _tenantId });
            return await db.SaveChangesAsync();
        });
        await MemberAsync("boss", UserType.CompanyAdmin);
        using var boss = await AsAsync("boss");

        Assert.Equal(HttpStatusCode.Forbidden, (await boss.PostAsync($"/api/User/{shared}/reset-password", null)).StatusCode);
    }

    [Theory]
    [InlineData("Administrator")]
    [InlineData("tenant-admin")]
    [InlineData("Company_Admin")]
    [InlineData("  ROOT ")]
    [InlineData("Super Admin")]
    public async Task A_tenant_cannot_create_or_rename_a_role_to_a_reserved_name(string name)
    {
        using var tenantAdmin = ClientFor(tenantId: 1, userId: 5);
        await CreateTenantAsync();
        var created = await tenantAdmin.PostAsJsonAsync("/api/Role", new { name });
        var ordinary = (await Read(await tenantAdmin.PostAsJsonAsync("/api/Role", new { name = "Nurse" }))).GetProperty("data").GetProperty("id").GetInt64();
        var renamed = await tenantAdmin.PutAsJsonAsync($"/api/Role/{ordinary}", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, renamed.StatusCode);
    }

    [Fact]
    public async Task A_platform_administrator_may_use_a_reserved_name_and_ordinary_names_are_free()
    {
        using var tenantAdmin = ClientFor(tenantId: 1, userId: 5);
        await CreateTenantAsync();

        Assert.Equal(HttpStatusCode.Created, (await Client.PostAsJsonAsync("/api/Role", new { name = "Administrator" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await tenantAdmin.PostAsJsonAsync("/api/Role", new { name = "Administration clerk" })).StatusCode);
        Assert.True(ReservedRoleNames.IsReserved("Tenant Administrator"));
        Assert.False(ReservedRoleNames.IsReserved("Nurse"));
    }

    [Fact]
    public async Task A_reader_can_look_but_not_change_anything()
    {
        await SetUpAsync();
        await MemberAsync("reader", UserType.Reader);
        using var reader = await AsAsync("reader");

        var write = await reader.PostAsJsonAsync("/api/Country", new { name = "X", code = "XX", formatCode = "x" });

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/Country")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("READ_ONLY", await ErrorCodeAsync(write));
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.DeleteAsync("/api/Country/1")).StatusCode);
    }

    [Fact]
    public async Task A_reader_can_still_manage_their_own_session_and_password()
    {
        await SetUpAsync();
        await MemberAsync("reader", UserType.Reader);
        using var reader = await AsAsync("reader");

        var changed = await reader.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = Password, newPassword = "Brand-new-1" });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        using var again = Bearer((await SignInAsync("reader", "Brand-new-1")).GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await again.PostAsync("/api/Auth/logout", null)).StatusCode);
    }

    [Fact]
    public async Task A_user_with_no_membership_is_read_only_too_and_a_platform_administrator_never_is()
    {
        await SetUpAsync();
        await CreateSignInUserAsync("drifter", _tenantId);
        await CreateSignInUserAsync("root", platformAdmin: true);
        using var drifter = await AsAsync("drifter");
        using var root = await AsAsync("root");

        Assert.Equal("READ_ONLY", await ErrorCodeAsync(await drifter.PostAsJsonAsync("/api/Country", new { name = "X", code = "XX", formatCode = "x" })));
        Assert.Equal(HttpStatusCode.Created, (await root.PostAsJsonAsync("/api/Country", new { name = "X", code = "XX", formatCode = "x" })).StatusCode);
    }

    [Fact]
    public async Task A_standard_member_is_not_a_reader_and_gets_as_far_as_the_permission_check()
    {
        await SetUpAsync();
        await MemberAsync("staff", UserType.Standard);
        using var staff = await AsAsync("staff");

        var write = await staff.PostAsJsonAsync("/api/Country", new { name = "X", code = "XX", formatCode = "x" });

        Assert.Equal("PERMISSION_DENIED", await ErrorCodeAsync(write));
    }
}
