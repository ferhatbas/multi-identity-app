using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class UserApiTests : ApiTestBase
{
    public UserApiTests()
    {
        CreateTenantAsync("T1").GetAwaiter().GetResult();
        CreateTenantAsync("T2").GetAwaiter().GetResult();
    }

    private static object NewUser(string userName = "grace", string password = "Passw0rd!", object? extra = null) => new
    {
        userName,
        email = $"{userName}@example.test",
        firstName = "Grace",
        lastName = "Hopper",
        password,
    };

    private async Task<long> CreateAsync(HttpClient client, string userName = "grace")
    {
        var response = await client.PostAsJsonAsync("/api/User", NewUser(userName));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read(response)).GetProperty("data").GetProperty("id").GetInt64();
    }

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public async Task A_tenants_administrator_creates_a_user_inside_their_tenant()
    {
        using var admin = ClientFor(tenantId: 1, userId: 1);

        var response = await admin.PostAsJsonAsync("/api/User", new
        {
            userName = " Grace ", email = "Grace@Example.test", firstName = " Grace ", lastName = "Hopper", password = "Passw0rd!",
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("grace", data.GetProperty("userName").GetString());
        Assert.Equal("grace@example.test", data.GetProperty("email").GetString());
        Assert.Equal("Grace", data.GetProperty("firstName").GetString());
        Assert.Equal(1, data.GetProperty("tenantId").GetInt64());
        Assert.True(data.GetProperty("shouldChangePasswordOnNextLogin").GetBoolean());
        Assert.False(data.GetProperty("isPlatformAdmin").GetBoolean());
    }

    [Fact]
    public async Task The_response_has_no_password_or_hash_and_the_stored_one_is_a_hash()
    {
        var response = await Client.PostAsJsonAsync("/api/User", new
        {
            userName = "grace", email = "g@example.test", firstName = "G", lastName = "H", password = "Passw0rd!", tenantId = 1,
        });
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("Passw0rd!", text);
        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        var stored = await InDbAsync(db => db.Users.Select(u => u.PasswordHash).SingleAsync());
        Assert.StartsWith("$2", stored);
    }

    [Fact]
    public async Task The_new_user_can_sign_in_with_the_password_that_was_set()
    {
        using var admin = ClientFor(tenantId: 1);
        await CreateAsync(admin);

        var data = await SignInAsync("grace");

        Assert.True(data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task A_weak_password_a_bad_e_mail_and_missing_fields_are_400()
    {
        using var admin = ClientFor(tenantId: 1);

        var weak = await admin.PostAsJsonAsync("/api/User", NewUser(password: "short"));
        var badMail = await admin.PostAsJsonAsync("/api/User",
            new { userName = "g", email = "not-an-email", firstName = "G", lastName = "H", password = "Passw0rd!" });
        var missing = await admin.PostAsJsonAsync("/api/User", new { userName = "g" });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badMail.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task User_names_and_e_mails_are_unique_across_tenants()
    {
        using var acme = ClientFor(tenantId: 1);
        using var globex = ClientFor(tenantId: 2);
        await CreateAsync(acme, "grace");

        var sameName = await globex.PostAsJsonAsync("/api/User", NewUser("grace"));
        var sameMail = await globex.PostAsJsonAsync("/api/User",
            new { userName = "other", email = "grace@example.test", firstName = "G", lastName = "H", password = "Passw0rd!" });

        Assert.Equal(HttpStatusCode.Conflict, sameName.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, sameMail.StatusCode);
        Assert.Contains("user name 'grace' already exists", (await Read(sameName)).GetProperty("message").GetString());
        Assert.Contains("email 'grace@example.test' already exists", (await Read(sameMail)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Without_an_active_tenant_the_tenant_must_be_named_and_must_exist()
    {
        var tenantId = await CreateTenantAsync();

        var missing = await Client.PostAsJsonAsync("/api/User", NewUser("a"));
        var unknown = await Client.PostAsJsonAsync("/api/User", new
        {
            userName = "b", email = "b@example.test", firstName = "B", lastName = "B", password = "Passw0rd!", tenantId = 999,
        });
        var good = await Client.PostAsJsonAsync("/api/User", new
        {
            userName = "c", email = "c@example.test", firstName = "C", lastName = "C", password = "Passw0rd!", tenantId,
        });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
    }

    [Fact]
    public async Task A_tenant_user_cannot_create_a_user_in_another_tenant()
    {
        using var acme = ClientFor(tenantId: 1);

        var response = await acme.PostAsJsonAsync("/api/User", new
        {
            userName = "x", email = "x@example.test", firstName = "X", lastName = "X", password = "Passw0rd!", tenantId = 2,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_tenant_sees_only_its_own_users_and_can_search_and_sort_them()
    {
        using var acme = ClientFor(tenantId: 1);
        using var globex = ClientFor(tenantId: 2);
        var mine = await CreateAsync(acme, "ada");
        await CreateAsync(acme, "bob");
        var theirs = await CreateAsync(globex, "eve");

        var page = (await Read(await acme.GetAsync("/api/User/paged?search=ada&orderBy=userName"))).GetProperty("data");

        Assert.Equal(1, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal(mine, page.GetProperty("data")[0].GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await acme.GetAsync($"/api/User/{theirs}")).StatusCode);
        Assert.Equal(2, (await Read(await acme.GetAsync("/api/User"))).GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task A_user_can_be_updated_but_not_into_a_taken_name()
    {
        using var admin = ClientFor(tenantId: 1);
        var id = await CreateAsync(admin, "grace");
        await CreateAsync(admin, "ada");

        var renamed = await admin.PutAsJsonAsync($"/api/User/{id}", new
        {
            userName = "grace", email = "grace@example.test", firstName = "Amazing", lastName = "Grace", phoneNumber = "555",
        });
        var taken = await admin.PutAsJsonAsync($"/api/User/{id}", new
        {
            userName = "ada", email = "grace@example.test", firstName = "X", lastName = "Y",
        });

        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        Assert.Equal("Amazing", (await Read(renamed)).GetProperty("data").GetProperty("firstName").GetString());
        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
    }

    [Fact]
    public async Task An_update_cannot_change_the_password_or_make_someone_a_platform_administrator()
    {
        using var admin = ClientFor(tenantId: 1);
        var id = await CreateAsync(admin, "grace");
        var before = await InDbAsync(db => db.Users.Select(u => u.PasswordHash).SingleAsync());

        await admin.PutAsJsonAsync($"/api/User/{id}", new
        {
            userName = "grace", email = "grace@example.test", firstName = "G", lastName = "H",
            password = "Hacked-123", passwordHash = "x", isPlatformAdmin = true,
        });

        Assert.Equal(before, await InDbAsync(db => db.Users.Select(u => u.PasswordHash).SingleAsync()));
        Assert.False(await InDbAsync(db => db.Users.Select(u => u.IsPlatformAdmin).SingleAsync()));
    }

    [Fact]
    public async Task Switching_a_user_off_signs_them_out_at_once()
    {
        using var admin = ClientFor(tenantId: 1, userId: 99);
        var id = await CreateAsync(admin, "grace");
        using var grace = Bearer((await SignInAsync("grace")).GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await grace.GetAsync("/api/Auth/me")).StatusCode);

        var off = await admin.PutAsJsonAsync($"/api/User/{id}", new
        {
            userName = "grace", email = "grace@example.test", firstName = "G", lastName = "H", isActive = false,
        });

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await grace.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.PostAsJsonAsync("/api/Auth/login", new { userName = "grace", password = "Passw0rd!" })).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_user_signs_them_out_and_restoring_lets_them_back()
    {
        using var admin = ClientFor(tenantId: 1, userId: 99);
        var id = await CreateAsync(admin, "grace");
        using var grace = Bearer((await SignInAsync("grace")).GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/User/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await grace.GetAsync("/api/Auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/User/{id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await grace.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await Client.PostAsJsonAsync("/api/Auth/login", new { userName = "grace", password = "Passw0rd!" })).StatusCode);
    }

    [Fact]
    public async Task Nobody_can_delete_or_deactivate_their_own_account()
    {
        using var setup = ClientFor(tenantId: 1);
        var id = await CreateAsync(setup, "grace");
        using var self = ClientFor(tenantId: 1, userId: id);

        var delete = await self.DeleteAsync($"/api/User/{id}");
        var off = await self.PutAsJsonAsync($"/api/User/{id}", new
        {
            userName = "grace", email = "grace@example.test", firstName = "G", lastName = "H", isActive = false,
        });

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, off.StatusCode);
    }

    [Fact]
    public async Task A_user_with_a_membership_cannot_be_deleted_until_it_is_removed()
    {
        var companyId = await CreateCompanyAsync(await CreateTenantAsync());
        var userId = await CreateUserAsync("ada");
        var membership = await PostIdAsync("/api/UserCompany", new { userId, companyId });

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/User/{userId}")).StatusCode);

        await Client.DeleteAsync($"/api/UserCompany/{membership}");
        Assert.Equal(HttpStatusCode.OK, (await Client.DeleteAsync($"/api/User/{userId}")).StatusCode);
    }

    [Fact]
    public async Task The_active_company_and_the_password_flag_can_be_edited_and_the_company_must_be_one_they_belong_to()
    {
        var tenantId = await CreateTenantAsync("ACT");
        var home = await CreateCompanyAsync(tenantId, "HOME");
        var other = await CreateCompanyAsync(tenantId, "OTHER");
        var userId = await CreateUserAsync("ada", tenantId);
        await PostIdAsync("/api/UserCompany", new { userId, companyId = home });
        object Body(long? activeCompanyId, bool? change) => new
        {
            userName = "ada", email = "ada@example.test", firstName = "Ada", lastName = "L", activeCompanyId, shouldChangePasswordOnNextLogin = change,
        };

        var notMember = await Client.PutAsJsonAsync($"/api/User/{userId}", Body(other, null));
        var member = await Client.PutAsJsonAsync($"/api/User/{userId}", Body(home, false));
        var kept = await Client.PutAsJsonAsync($"/api/User/{userId}", Body(home, null));

        Assert.Equal(HttpStatusCode.BadRequest, notMember.StatusCode);
        Assert.Equal(HttpStatusCode.OK, member.StatusCode);
        var data = (await Read(kept)).GetProperty("data");
        Assert.Equal(home, data.GetProperty("activeCompanyId").GetInt64());
        Assert.False(data.GetProperty("shouldChangePasswordOnNextLogin").GetBoolean());
    }

    [Fact]
    public async Task Managing_users_needs_the_user_permissions()
    {
        using var nobody = ClientFor(tenantId: 1, userId: 5, permissions: "none");
        using var viewer = ClientFor(tenantId: 1, userId: 6, permissions: Permissions.UserView);

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("/api/User")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/User")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/User", NewUser())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/User/import", new { users = new[] { new { userName = "x" } } })).StatusCode);
    }

    [Fact]
    public async Task Effective_permissions_say_where_each_permission_comes_from()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);
        var userId = await CreateSignInUserAsync("ada", tenantId, companyId);
        var read = await CreatePermissionAsync("USER_VIEW");
        var write = await CreatePermissionAsync("USER_MANAGE");
        var extra = await CreatePermissionAsync("ROLE_VIEW");
        var roleId = await CreateRoleAsync("Clerk");
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId = read });
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId = write });
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId, roleId });
        await PostIdAsync("/api/UserCompanyPermission", new { userId, companyId, permissionId = write, isGranted = false });
        await PostIdAsync("/api/UserCompanyPermission", new { userId, companyId, permissionId = extra, isGranted = true });

        var data = (await Read(await Client.GetAsync($"/api/User/{userId}/effective-permissions?companyId={companyId}"))).GetProperty("data");

        var granted = data.GetProperty("granted").EnumerateArray().ToDictionary(
            p => p.GetProperty("code").GetString()!,
            p => p.GetProperty("via").EnumerateArray().Select(v => v.GetString()).ToArray());
        Assert.Equal(["ROLE_VIEW", "USER_VIEW"], granted.Keys.Order());
        Assert.Equal(["role:Clerk"], granted["USER_VIEW"]);
        Assert.Equal(["direct"], granted["ROLE_VIEW"]);
        Assert.Equal(["USER_MANAGE"], data.GetProperty("denied").EnumerateArray().Select(d => d.GetString()));
    }

    [Fact]
    public async Task Effective_permissions_of_a_user_with_no_company_are_empty_and_another_tenants_user_is_404()
    {
        var mine = await CreateUserAsync("ada", tenantId: 1);
        var theirs = await CreateUserAsync("eve", tenantId: 2);
        using var acme = ClientFor(tenantId: 1);

        var empty = (await Read(await acme.GetAsync($"/api/User/{mine}/effective-permissions"))).GetProperty("data");

        Assert.Empty(empty.GetProperty("granted").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await acme.GetAsync($"/api/User/{theirs}/effective-permissions")).StatusCode);
    }

    private async Task<(long UserId, long CompanyId, long RoleId, long PermissionId)> UserWithAccessAsync(long tenantId)
    {
        var companyId = await CreateCompanyAsync(tenantId);
        var userId = await CreateSignInUserAsync("ada", tenantId, companyId);
        var permissionId = await CreatePermissionAsync("USER_VIEW");
        var roleId = await CreateRoleAsync("Clerk");
        await PostIdAsync("/api/UserCompanyRole", new { userId, companyId, roleId });
        await PostIdAsync("/api/UserCompanyPermission", new { userId, companyId, permissionId, isGranted = true });
        return (userId, companyId, roleId, permissionId);
    }

    [Fact]
    public async Task A_copy_has_the_same_memberships_roles_and_permissions_and_a_generated_password_that_works()
    {
        var tenantId = await CreateTenantAsync();
        var (userId, companyId, roleId, _) = await UserWithAccessAsync(tenantId);

        var response = await Client.PostAsJsonAsync($"/api/User/{userId}/copy", new { userName = "Twin", email = "twin@example.test" });
        var data = (await Read(response)).GetProperty("data");
        var temporary = data.GetProperty("temporaryPassword").GetString()!;
        var copyId = data.GetProperty("user").GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("twin", data.GetProperty("user").GetProperty("userName").GetString());
        Assert.Equal(tenantId, data.GetProperty("user").GetProperty("tenantId").GetInt64());
        Assert.True(data.GetProperty("user").GetProperty("shouldChangePasswordOnNextLogin").GetBoolean());
        Assert.Equal(1, await InDbAsync(db => db.UserCompanies.CountAsync(m => m.UserId == copyId && m.CompanyId == companyId)));
        Assert.Equal(1, await InDbAsync(db => db.UserCompanyRoles.CountAsync(r => r.UserId == copyId && r.RoleId == roleId)));
        Assert.Equal(1, await InDbAsync(db => db.UserCompanyPermissions.CountAsync(p => p.UserId == copyId)));
        Assert.Equal(HttpStatusCode.OK,
            (await Client.PostAsJsonAsync("/api/Auth/login", new { userName = "twin", password = temporary })).StatusCode);
    }

    [Fact]
    public async Task A_copy_with_a_chosen_password_does_not_echo_it_and_can_leave_things_out()
    {
        var (userId, _, _, _) = await UserWithAccessAsync(await CreateTenantAsync());

        var response = await Client.PostAsJsonAsync($"/api/User/{userId}/copy", new
        {
            userName = "twin", email = "twin@example.test", password = "Chosen-pass-1",
            copyMemberships = false, copyRoles = false, copyPermissions = false,
        });
        var data = (await Read(response)).GetProperty("data");
        var copyId = data.GetProperty("user").GetProperty("id").GetInt64();

        Assert.Equal(JsonValueKind.Null, data.GetProperty("temporaryPassword").ValueKind);
        Assert.False(data.GetProperty("user").GetProperty("shouldChangePasswordOnNextLogin").GetBoolean());
        Assert.Equal(0, await InDbAsync(db => db.UserCompanies.CountAsync(m => m.UserId == copyId)));
        Assert.Equal(0, await InDbAsync(db => db.UserCompanyRoles.CountAsync(r => r.UserId == copyId)));
    }

    [Fact]
    public async Task A_copy_needs_free_names_and_a_visible_source()
    {
        var tenantId = await CreateTenantAsync();
        var (userId, _, _, _) = await UserWithAccessAsync(tenantId);
        var other = await CreateUserAsync("eve", tenantId: tenantId + 100);
        using var acme = ClientFor(tenantId: tenantId);

        var taken = await Client.PostAsJsonAsync($"/api/User/{userId}/copy", new { userName = "ada", email = "twin@example.test" });
        var mailTaken = await Client.PostAsJsonAsync($"/api/User/{userId}/copy", new { userName = "twin", email = "ada@example.test" });
        var invisible = await acme.PostAsJsonAsync($"/api/User/{other}/copy", new { userName = "twin", email = "twin@example.test" });

        Assert.Equal(HttpStatusCode.Conflict, taken.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, mailTaken.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, invisible.StatusCode);
    }

    [Fact]
    public async Task A_copied_role_has_the_same_permissions_and_a_new_name()
    {
        var roleId = await CreateRoleAsync("Nurse");
        var permissionId = await CreatePermissionAsync("USER_VIEW");
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId });

        var response = await Client.PostAsJsonAsync($"/api/Role/{roleId}/copy", new { name = "Senior nurse", code = "senior" });
        var data = (await Read(response)).GetProperty("data");
        var copyId = data.GetProperty("id").GetInt64();
        var again = await Client.PostAsJsonAsync($"/api/Role/{roleId}/copy", new { name = "Senior nurse" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("SENIOR", data.GetProperty("code").GetString());
        Assert.Equal(1, await InDbAsync(db => db.RolePermissions.CountAsync(g => g.RoleId == copyId && g.PermissionId == permissionId)));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.PostAsJsonAsync("/api/Role/999/copy", new { name = "X" })).StatusCode);
    }

    private async Task<(long TenantId, long CompanyId, long RoleId)> ImportSetupAsync()
    {
        var tenantId = await CreateTenantAsync();
        return (tenantId, await CreateCompanyAsync(tenantId), await CreateRoleAsync("Clerk"));
    }

    [Fact]
    public async Task An_import_creates_the_users_with_their_company_and_roles_and_returns_generated_passwords_once()
    {
        var (tenantId, companyId, roleId) = await ImportSetupAsync();

        var response = await Client.PostAsJsonAsync("/api/User/import", new
        {
            tenantId,
            users = new object[]
            {
                new { userName = "one", email = "one@example.test", firstName = "One", lastName = "A", companyId, roleIds = new[] { roleId } },
                new { userName = "two", email = "two@example.test", firstName = "Two", lastName = "B", password = "Chosen-pass-1" },
            },
        });
        var data = (await Read(response)).GetProperty("data");
        var one = data.GetProperty("users")[0];
        var two = data.GetProperty("users")[1];

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, data.GetProperty("created").GetInt32());
        Assert.Empty(data.GetProperty("failed").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, two.GetProperty("temporaryPassword").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/Auth/login",
            new { userName = "one", password = one.GetProperty("temporaryPassword").GetString() })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Client.PostAsJsonAsync("/api/Auth/login",
            new { userName = "two", password = "Chosen-pass-1" })).StatusCode);
        Assert.Equal(1, await InDbAsync(db => db.UserCompanyRoles.CountAsync(r => r.RoleId == roleId)));
        Assert.Equal(1, await InDbAsync(db => db.UserCompanies.CountAsync(m => m.CompanyId == companyId)));
    }

    [Fact]
    public async Task A_row_that_is_wrong_is_reported_with_its_number_and_the_good_rows_still_go_in()
    {
        var (tenantId, _, _) = await ImportSetupAsync();
        await CreateUserAsync("taken");

        var response = await Client.PostAsJsonAsync("/api/User/import", new
        {
            tenantId,
            users = new object[]
            {
                new { userName = "fine", email = "fine@example.test", firstName = "F", lastName = "F" },
                new { userName = "taken", email = "new@example.test", firstName = "T", lastName = "T" },
                new { userName = "fine", email = "other@example.test", firstName = "D", lastName = "D" },
                new { userName = "nomail", email = "nope", firstName = "N", lastName = "N" },
                new { userName = "weak", email = "weak@example.test", firstName = "W", lastName = "W", password = "short" },
                new { userName = "nocompany", email = "nc@example.test", firstName = "N", lastName = "C", companyId = 999 },
            },
        });
        var data = (await Read(response)).GetProperty("data");
        var failed = data.GetProperty("failed").EnumerateArray().ToDictionary(f => f.GetProperty("row").GetInt32(), f => f.GetProperty("error").GetString()!);

        Assert.Equal(1, data.GetProperty("created").GetInt32());
        Assert.Contains("taken", failed[2]);
        Assert.Contains("already taken", failed[3]);
        Assert.Contains("email", failed[4]);
        Assert.Contains("at least 8", failed[5]);
        Assert.Contains("Company 999", failed[6]);
    }

    [Fact]
    public async Task An_atomic_import_takes_nothing_when_one_row_is_wrong()
    {
        var (tenantId, _, _) = await ImportSetupAsync();

        var response = await Client.PostAsJsonAsync("/api/User/import", new
        {
            tenantId,
            atomic = true,
            users = new object[]
            {
                new { userName = "fine", email = "fine@example.test", firstName = "F", lastName = "F" },
                new { userName = "bad", email = "nope", firstName = "B", lastName = "B" },
            },
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(0, data.GetProperty("created").GetInt32());
        Assert.True(data.GetProperty("rolledBack").GetBoolean());
        Assert.Single(data.GetProperty("failed").EnumerateArray());
        Assert.Equal(0, await InDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task An_atomic_import_whose_late_row_fails_rolls_the_earlier_rows_back()
    {
        var (tenantId, _, _) = await ImportSetupAsync();

        var response = await Client.PostAsJsonAsync("/api/User/import", new
        {
            tenantId,
            atomic = true,
            users = new object[]
            {
                new { userName = "fine", email = "fine@example.test", firstName = "F", lastName = "F", password = "Chosen-pass-1" },
                new { userName = "weak", email = "weak@example.test", firstName = "W", lastName = "W", password = "short" },
            },
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.True(data.GetProperty("rolledBack").GetBoolean());
        Assert.Equal(0, await InDbAsync(db => db.Users.CountAsync()));
    }

    [Fact]
    public async Task An_import_stays_inside_the_callers_tenant_and_is_bounded()
    {
        using var acme = ClientFor(tenantId: 1);
        var foreign = await acme.PostAsJsonAsync("/api/User/import", new
        {
            tenantId = 2, users = new[] { new { userName = "x", email = "x@example.test", firstName = "X", lastName = "X" } },
        });
        var empty = await acme.PostAsJsonAsync("/api/User/import", new { users = Array.Empty<object>() });
        var tooMany = await acme.PostAsJsonAsync("/api/User/import", new
        {
            users = Enumerable.Range(0, 501).Select(i => new { userName = $"u{i}", email = $"u{i}@example.test", firstName = "U", lastName = "U" }),
        });

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooMany.StatusCode);
    }

    [Fact]
    public async Task Temporary_passwords_obey_the_password_rules_and_differ()
    {
        var (tenantId, _, _) = await ImportSetupAsync();

        var response = await Client.PostAsJsonAsync("/api/User/import", new
        {
            tenantId,
            users = Enumerable.Range(0, 5).Select(i => new { userName = $"u{i}", email = $"u{i}@example.test", firstName = "U", lastName = "U" }),
        });
        var passwords = (await Read(response)).GetProperty("data").GetProperty("users").EnumerateArray()
            .Select(u => u.GetProperty("temporaryPassword").GetString()!).ToList();

        Assert.Equal(5, passwords.Distinct().Count());
        Assert.All(passwords, p =>
        {
            Assert.True(p.Length >= 12);
            Assert.Contains(p, char.IsLetter);
            Assert.Contains(p, char.IsDigit);
        });
    }
}
