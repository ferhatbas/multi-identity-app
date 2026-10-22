using System.Net;
using System.Net.Http.Json;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class AccessApiTests : ApiTestBase
{
    [Fact]
    public async Task A_permission_code_is_trimmed_upper_cased_and_unique()
    {
        var response = await Client.PostAsJsonAsync("/api/Permission", new { name = "View users", code = " user_view " });
        var data = (await Read(response)).GetProperty("data");

        var again = await Client.PostAsJsonAsync("/api/Permission", new { name = "Other", code = "USER_VIEW" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("USER_VIEW", data.GetProperty("code").GetString());
        Assert.False(data.GetProperty("isManagement").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_role_belongs_to_the_tenant_that_created_it()
    {
        using var acme = ClientFor(tenantId: 1);
        using var globex = ClientFor(tenantId: 2);
        var created = await acme.PostAsJsonAsync("/api/Role", new { name = "Nurse", code = "nurse" });
        var data = (await Read(created)).GetProperty("data");
        var id = data.GetProperty("id").GetInt64();

        Assert.Equal(1, data.GetProperty("tenantId").GetInt64());
        Assert.Equal("NURSE", data.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await acme.GetAsync($"/api/Role/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/Role/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_role_name_is_unique_within_a_tenant_but_two_tenants_may_share_it()
    {
        using var acme = ClientFor(tenantId: 1);
        using var globex = ClientFor(tenantId: 2);
        await acme.PostAsJsonAsync("/api/Role", new { name = "Nurse" });

        var sameTenant = await acme.PostAsJsonAsync("/api/Role", new { name = "Nurse" });
        var otherTenant = await globex.PostAsJsonAsync("/api/Role", new { name = "Nurse" });

        Assert.Equal(HttpStatusCode.Conflict, sameTenant.StatusCode);
        Assert.Equal(HttpStatusCode.Created, otherTenant.StatusCode);
    }

    [Fact]
    public async Task A_role_name_is_unique_per_application_so_each_application_has_its_own_cashier()
    {
        using var acme = ClientFor(tenantId: 1);
        var retail = await PostIdAsync("/api/ApplicationType", new { name = "Retail", code = "RETAIL" });
        var hotel = await PostIdAsync("/api/ApplicationType", new { name = "Hotel", code = "HOTEL" });

        var retailCashier = await acme.PostAsJsonAsync("/api/Role", new { name = "Cashier", applicationTypeId = retail });
        var hotelCashier = await acme.PostAsJsonAsync("/api/Role", new { name = "Cashier", applicationTypeId = hotel });
        var sharedCashier = await acme.PostAsJsonAsync("/api/Role", new { name = "Cashier" });
        var again = await acme.PostAsJsonAsync("/api/Role", new { name = "Cashier", applicationTypeId = retail });
        var againShared = await acme.PostAsJsonAsync("/api/Role", new { name = "Cashier" });

        Assert.Equal(HttpStatusCode.Created, retailCashier.StatusCode);
        Assert.Equal(HttpStatusCode.Created, hotelCashier.StatusCode);
        Assert.Equal(HttpStatusCode.Created, sharedCashier.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, againShared.StatusCode);
    }

    [Fact]
    public async Task A_role_can_be_limited_to_an_application_that_exists()
    {
        var applicationId = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });

        var good = await Client.PostAsJsonAsync("/api/Role", new { name = "Doctor", applicationTypeId = applicationId });
        var bad = await Client.PostAsJsonAsync("/api/Role", new { name = "Ghost", applicationTypeId = 999 });

        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task A_role_can_be_given_a_permission_once()
    {
        var roleId = await CreateRoleAsync();
        var permissionId = await CreatePermissionAsync();
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId });

        var again = await Client.PostAsJsonAsync("/api/RolePermission", new { roleId, permissionId });
        var missing = await Client.PostAsJsonAsync("/api/RolePermission", new { roleId, permissionId = 999 });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task A_role_or_permission_that_is_in_use_cannot_be_deleted()
    {
        var roleId = await CreateRoleAsync();
        var permissionId = await CreatePermissionAsync();
        var grantId = await PostIdAsync("/api/RolePermission", new { roleId, permissionId });

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/Role/{roleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/Permission/{permissionId}")).StatusCode);

        await Client.DeleteAsync($"/api/RolePermission/{grantId}");
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/api/Role/{roleId}")).StatusCode);
    }

    [Fact]
    public async Task A_user_joins_a_company_once_with_a_rank()
    {
        var userId = await CreateUserAsync();
        var companyId = await CreateCompanyAsync(await CreateTenantAsync());

        var joined = await Client.PostAsJsonAsync("/api/UserCompany", new { userId, companyId, membershipType = 2 });
        var again = await Client.PostAsJsonAsync("/api/UserCompany", new { userId, companyId });
        var data = (await Read(joined)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, joined.StatusCode);
        Assert.Equal(2, data.GetProperty("membershipType").GetInt32());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_membership_needs_a_real_user_and_company()
    {
        var userId = await CreateUserAsync();

        var noCompany = await Client.PostAsJsonAsync("/api/UserCompany", new { userId, companyId = 999 });
        var noUser = await Client.PostAsJsonAsync("/api/UserCompany",
            new { userId = 999, companyId = await CreateCompanyAsync(await CreateTenantAsync()) });

        Assert.Equal(HttpStatusCode.BadRequest, noCompany.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noUser.StatusCode);
    }

    [Fact]
    public async Task A_company_with_members_cannot_be_deleted()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);
        var userId = await CreateUserAsync();
        await PostIdAsync("/api/UserCompany", new { userId, companyId });

        var response = await Client.DeleteAsync($"/api/Company/{companyId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task The_same_role_may_be_held_in_two_companies_but_only_once_in_each()
    {
        var tenantId = await CreateTenantAsync();
        var first = await CreateCompanyAsync(tenantId, "A");
        var second = await CreateCompanyAsync(tenantId, "B");
        var userId = await CreateUserAsync();
        var roleId = await CreateRoleAsync();
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId = first, roleId });

        var otherCompany = await Client.PostAsJsonAsync("/api/UserCompanyRole", new { userId, companyId = second, roleId });
        var duplicate = await Client.PostAsJsonAsync("/api/UserCompanyRole", new { userId, companyId = first, roleId });

        Assert.Equal(HttpStatusCode.Created, otherCompany.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task A_permission_can_be_granted_or_taken_away_per_user_and_company()
    {
        var companyId = await CreateCompanyAsync(await CreateTenantAsync());
        var userId = await CreateUserAsync();
        var permissionId = await CreatePermissionAsync();

        var denied = await Client.PostAsJsonAsync("/api/UserCompanyPermission",
            new { userId, companyId, permissionId, isGranted = false });
        var data = (await Read(denied)).GetProperty("data");
        var again = await Client.PostAsJsonAsync("/api/UserCompanyPermission", new { userId, companyId, permissionId });

        Assert.Equal(HttpStatusCode.Created, denied.StatusCode);
        Assert.False(data.GetProperty("isGranted").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_users_password_hash_never_reaches_the_audit_trail()
    {
        await CreateUserAsync("grace");

        using var scope = Factory.Services.CreateScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .AuditLogs.Where(a => a.EntityName == "User").ToListAsync();

        var row = Assert.Single(rows);
        Assert.Contains("grace", row.NewValues);
        Assert.DoesNotContain("not-a-real-hash", row.NewValues);
        Assert.DoesNotContain("PasswordHash", row.NewValues);
    }

    [Fact]
    public async Task Two_users_cannot_share_a_user_name_or_e_mail_even_in_different_tenants()
    {
        await CreateUserAsync("ada", tenantId: 1);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Users.Add(new FSIdentity.Domain.Entities.Accounts.User
        {
            UserName = "ada", Email = "other@example.test", FirstName = "A", LastName = "B", PasswordHash = "x", IdentityTenantId = 2,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
