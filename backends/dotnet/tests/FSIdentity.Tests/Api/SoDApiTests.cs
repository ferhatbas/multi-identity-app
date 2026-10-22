using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Entities.Accounts;

namespace FSIdentity.Tests.Api;

public class SoDApiTests : ApiTestBase
{
    private long _create;
    private long _approve;
    private long _report;
    private long _tenantId;
    private long _companyId;

    private async Task SetUpAsync()
    {
        _create = await CreatePermissionAsync("PAYMENT_CREATE");
        _approve = await CreatePermissionAsync("PAYMENT_APPROVE");
        _report = await CreatePermissionAsync("PAYMENT_REPORT");
        _tenantId = await CreateTenantAsync();
        _companyId = await CreateCompanyAsync(_tenantId);
    }

    private Task<long> RuleAsync(long a, long b, string name = "Payments") =>
        PostIdAsync("/api/SoDRule", new { name, permissionAId = a, permissionBId = b });

    private async Task<long> RoleHoldingAsync(string name, params long[] permissions)
    {
        var roleId = await CreateRoleAsync(name);
        foreach (var permissionId in permissions)
            await PostIdAsync("/api/RolePermission", new { roleId, permissionId });
        return roleId;
    }

    private async Task<string> ErrorCodeAsync(HttpResponseMessage response)
        => (await Read(response)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    [Fact]
    public async Task A_rule_names_two_different_existing_permissions_and_is_not_repeated()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);

        var same = await Client.PostAsJsonAsync("/api/SoDRule", new { name = "x", permissionAId = _create, permissionBId = _create });
        var missing = await Client.PostAsJsonAsync("/api/SoDRule", new { name = "x", permissionAId = _create, permissionBId = 999 });
        var again = await Client.PostAsJsonAsync("/api/SoDRule", new { name = "again", permissionAId = _create, permissionBId = _approve });

        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_permission_that_is_part_of_a_rule_cannot_be_deleted()
    {
        await SetUpAsync();
        var ruleId = await RuleAsync(_create, _approve);

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/Permission/{_create}")).StatusCode);

        await Client.DeleteAsync($"/api/SoDRule/{ruleId}");
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/api/Permission/{_create}")).StatusCode);
    }

    [Fact]
    public async Task A_new_rule_is_refused_when_a_role_already_holds_both_permissions()
    {
        await SetUpAsync();
        await RoleHoldingAsync("Both", _create, _approve);

        var response = await Client.PostAsJsonAsync("/api/SoDRule", new { name = "Payments", permissionAId = _create, permissionBId = _approve });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SOD_CONFLICT", await ErrorCodeAsync(response));
        Assert.Contains("Both", (await Read(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_role_cannot_come_to_hold_both_permissions_of_a_rule()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);
        var roleId = await RoleHoldingAsync("Clerk", _create);

        var response = await Client.PostAsJsonAsync("/api/RolePermission", new { roleId, permissionId = _approve });
        var fine = await Client.PostAsJsonAsync("/api/RolePermission", new { roleId, permissionId = _report });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SOD_CONFLICT", await ErrorCodeAsync(response));
        var message = (await Read(response)).GetProperty("message").GetString();
        Assert.Contains("PAYMENT_CREATE", message);
        Assert.Contains("PAYMENT_APPROVE", message);
        Assert.Contains("Payments", message);
        Assert.Equal(HttpStatusCode.Created, fine.StatusCode);
    }

    [Fact]
    public async Task Giving_a_role_a_permission_is_refused_when_somebody_who_holds_it_would_then_break_a_rule()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);
        var userId = await CreateUserAsync("ada", _tenantId);
        var approver = await RoleHoldingAsync("Approver", _approve);
        var clerk = await RoleHoldingAsync("Clerk", _report);
        await PostIdAsync("/api/UserCompany", new { userId, companyId = _companyId });
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId = approver });
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId = clerk });

        var response = await Client.PostAsJsonAsync("/api/RolePermission", new { roleId = clerk, permissionId = _create });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("This person", (await Read(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_person_cannot_hold_both_permissions_through_two_roles()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);
        var userId = await CreateUserAsync("ada", _tenantId);
        await PostIdAsync("/api/UserCompany", new { userId, companyId = _companyId });
        var creator = await RoleHoldingAsync("Creator", _create);
        var approver = await RoleHoldingAsync("Approver", _approve);
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId = creator });

        var response = await Client.PostAsJsonAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId = approver });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("SOD_CONFLICT", await ErrorCodeAsync(response));
    }

    [Fact]
    public async Task The_same_two_roles_are_fine_for_a_person_in_another_company_or_for_two_different_people()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);
        var secondCompany = await CreateCompanyAsync(_tenantId, "BRANCH");
        var ada = await CreateUserAsync("ada", _tenantId);
        var bob = await CreateUserAsync("bob", _tenantId);
        foreach (var user in new[] { ada, bob })
        {
            await PostIdAsync("/api/UserCompany", new { userId = user, companyId = _companyId });
        }
        await PostIdAsync("/api/UserCompany", new { userId = ada, companyId = secondCompany });
        var creator = await RoleHoldingAsync("Creator", _create);
        var approver = await RoleHoldingAsync("Approver", _approve);

        await PostIdAsync("/api/UserCompanyRole", new { userId = ada, companyId = _companyId, roleId = creator });
        var otherCompany = await Client.PostAsJsonAsync("/api/UserCompanyRole", new { userId = ada, companyId = secondCompany, roleId = approver });
        var otherPerson = await Client.PostAsJsonAsync("/api/UserCompanyRole", new { userId = bob, companyId = _companyId, roleId = approver });

        Assert.Equal(HttpStatusCode.Created, otherCompany.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherPerson.StatusCode);
    }

    [Fact]
    public async Task A_personal_grant_counts_as_well()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);
        var userId = await CreateUserAsync("ada", _tenantId);
        await PostIdAsync("/api/UserCompany", new { userId, companyId = _companyId });
        await PostIdAsync("/api/UserCompanyRole", new
        {
            userId, companyId = _companyId, roleId = await RoleHoldingAsync("Creator", _create),
        });

        var grant = await Client.PostAsJsonAsync("/api/UserCompanyPermission",
            new { userId, companyId = _companyId, permissionId = _approve, isGranted = true });
        var takeAway = await Client.PostAsJsonAsync("/api/UserCompanyPermission",
            new { userId, companyId = _companyId, permissionId = _approve, isGranted = false });

        Assert.Equal(HttpStatusCode.Conflict, grant.StatusCode);
        Assert.Equal("SOD_CONFLICT", await ErrorCodeAsync(grant));
        Assert.Equal(HttpStatusCode.Created, takeAway.StatusCode);
    }

    [Fact]
    public async Task Roles_for_different_applications_still_count_together_because_a_person_can_switch()
    {
        await SetUpAsync();
        await RuleAsync(_create, _approve);
        var hospital = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        var lab = await PostIdAsync("/api/ApplicationType", new { name = "Lab", code = "LAB" });
        var userId = await CreateUserAsync("ada", _tenantId);
        await PostIdAsync("/api/UserCompany", new { userId, companyId = _companyId });
        var creator = await PostIdAsync("/api/Role", new { name = "Hospital creator", applicationTypeId = hospital });
        await PostIdAsync("/api/RolePermission", new { roleId = creator, permissionId = _create });
        var approver = await PostIdAsync("/api/Role", new { name = "Lab approver", applicationTypeId = lab });
        await PostIdAsync("/api/RolePermission", new { roleId = approver, permissionId = _approve });
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId = creator });

        var response = await Client.PostAsJsonAsync("/api/UserCompanyRole", new { userId, companyId = _companyId, roleId = approver });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_rule_that_is_switched_off_or_deleted_stops_applying()
    {
        await SetUpAsync();
        var ruleId = await RuleAsync(_create, _approve);
        var roleId = await RoleHoldingAsync("Clerk", _create);

        await Client.PutAsJsonAsync($"/api/SoDRule/{ruleId}", new { name = "Payments", permissionAId = _create, permissionBId = _approve, isActive = false });

        Assert.Equal(HttpStatusCode.Created, (await Client.PostAsJsonAsync("/api/RolePermission", new { roleId, permissionId = _approve })).StatusCode);
    }

    [Fact]
    public async Task A_tenants_rule_binds_that_tenant_only_and_the_platforms_rule_binds_everybody()
    {
        await SetUpAsync();
        var other = await CreateTenantAsync("OTHER");
        using var tenantAdmin = ClientFor(tenantId: _tenantId, userId: 7);
        using var otherAdmin = ClientFor(tenantId: other, userId: 8);
        await tenantAdmin.PostAsJsonAsync("/api/SoDRule", new { name = "Ours", permissionAId = _create, permissionBId = _approve });

        var ours = await tenantAdmin.PostAsJsonAsync("/api/Role", new { name = "Ours both" });
        var oursId = (await Read(ours)).GetProperty("data").GetProperty("id").GetInt64();
        await tenantAdmin.PostAsJsonAsync("/api/RolePermission", new { roleId = oursId, permissionId = _create });
        var oursBlocked = await tenantAdmin.PostAsJsonAsync("/api/RolePermission", new { roleId = oursId, permissionId = _approve });

        var theirs = await otherAdmin.PostAsJsonAsync("/api/Role", new { name = "Theirs both" });
        var theirsId = (await Read(theirs)).GetProperty("data").GetProperty("id").GetInt64();
        await otherAdmin.PostAsJsonAsync("/api/RolePermission", new { roleId = theirsId, permissionId = _create });
        var theirsFine = await otherAdmin.PostAsJsonAsync("/api/RolePermission", new { roleId = theirsId, permissionId = _approve });

        Assert.Equal(HttpStatusCode.Conflict, oursBlocked.StatusCode);
        Assert.Equal(HttpStatusCode.Created, theirsFine.StatusCode);

        await RuleAsync(_create, _report, "Platform-wide");
        var wide = await otherAdmin.PostAsJsonAsync("/api/RolePermission", new { roleId = theirsId, permissionId = _report });
        Assert.Equal(HttpStatusCode.Conflict, wide.StatusCode);
    }

    [Fact]
    public async Task Rules_and_roles_need_the_security_and_role_permissions_to_change()
    {
        await SetUpAsync();
        using var nobody = ClientFor(tenantId: _tenantId, userId: 5, permissions: "none");

        Assert.Equal(HttpStatusCode.Forbidden,
            (await nobody.PostAsJsonAsync("/api/SoDRule", new { name = "x", permissionAId = _create, permissionBId = _approve })).StatusCode);
    }

    [Fact]
    public async Task Restoring_a_grant_that_would_now_break_a_rule_is_refused()
    {
        await SetUpAsync();
        var roleId = await RoleHoldingAsync("Clerk", _create);
        var grantId = await PostIdAsync("/api/RolePermission", new { roleId, permissionId = _approve });
        await Client.DeleteAsync($"/api/RolePermission/{grantId}");
        await RuleAsync(_create, _approve);

        var response = await Client.PostAsync($"/api/RolePermission/{grantId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
