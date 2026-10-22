using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class UserAdminApiTests : ApiTestBase
{
    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<HttpResponseMessage> LoginAsync(string userName, string password)
        => Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName, password });

    private async Task<(long TenantId, long CompanyId)> TenantAsync(string code = "ACME")
    {
        var tenantId = await CreateTenantAsync(code);
        return (tenantId, await CreateCompanyAsync(tenantId, $"HQ-{code}"));
    }

    private async Task<HttpClient> AdminFor(long tenantId, string permissions)
        => ClientFor(tenantId: tenantId, userId: 900, permissions: permissions);

    [Fact]
    public async Task An_administrator_resets_a_password_and_the_user_must_change_the_generated_one()
    {
        var (tenantId, _) = await TenantAsync();
        await CreateSignInUserAsync("ada", tenantId);
        var id = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        using var phone = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var admin = await AdminFor(tenantId, Permissions.UserManage);

        var response = await admin.PostAsync($"/api/User/{id}/reset-password", null);
        var temporary = (await Read(response)).GetProperty("data").GetProperty("password").GetString()!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("ada", Password)).StatusCode);
        var signedIn = await LoginAsync("ada", temporary);
        Assert.Equal(HttpStatusCode.OK, signedIn.StatusCode);
        Assert.True((await Read(signedIn)).GetProperty("data").GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.Contains(Emails.Sent, m => m.To == "ada@example.test" && m.Subject.Contains("password was changed"));
    }

    [Fact]
    public async Task A_reset_also_lifts_a_lockout()
    {
        var (tenantId, _) = await TenantAsync();
        await CreateSignInUserAsync("ada", tenantId);
        var id = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        for (var i = 0; i < 5; i++)
            await LoginAsync("ada", "wrong-pass-1");
        Assert.Equal((HttpStatusCode)423, (await LoginAsync("ada", Password)).StatusCode);
        using var admin = await AdminFor(tenantId, Permissions.UserManage);

        var temporary = (await Read(await admin.PostAsync($"/api/User/{id}/reset-password", null)))
            .GetProperty("data").GetProperty("password").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ada", temporary)).StatusCode);
    }

    [Fact]
    public async Task Unlocking_lets_the_user_in_with_the_old_password()
    {
        var (tenantId, _) = await TenantAsync();
        await CreateSignInUserAsync("ada", tenantId);
        var id = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        for (var i = 0; i < 5; i++)
            await LoginAsync("ada", "wrong-pass-1");
        using var admin = await AdminFor(tenantId, Permissions.UserManage);

        var response = await admin.PostAsync($"/api/User/{id}/unlock", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ada", Password)).StatusCode);
    }

    [Fact]
    public async Task These_actions_are_audited_and_need_the_user_permission()
    {
        var (tenantId, _) = await TenantAsync();
        await CreateSignInUserAsync("ada", tenantId);
        var id = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        using var admin = await AdminFor(tenantId, Permissions.UserManage);
        using var viewer = ClientFor(tenantId: tenantId, userId: 901, permissions: Permissions.UserView);

        await admin.PostAsync($"/api/User/{id}/reset-password", null);
        await admin.PostAsync($"/api/User/{id}/unlock", null);

        var actions = await InDbAsync(db => db.AuditLogs.Where(a => a.EntityName == "User" && a.EntityId == id.ToString())
            .OrderBy(a => a.Id).Select(a => a.Action).ToListAsync());
        Assert.Contains("AdminPasswordReset", actions);
        Assert.Contains("Unlock", actions);
        var reset = await InDbAsync(db => db.AuditLogs.SingleAsync(a => a.Action == "AdminPasswordReset"));
        Assert.Equal(900, reset.UserId);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/User/{id}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/User/{id}/unlock", null)).StatusCode);
    }

    [Fact]
    public async Task A_tenant_administrator_cannot_reach_another_tenants_user_or_a_platform_administrator()
    {
        var (acme, _) = await TenantAsync("ACME");
        var (globex, _) = await TenantAsync("GLOBEX");
        await CreateSignInUserAsync("eve", globex);
        var eve = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync(u => u != 0));
        await CreateSignInUserAsync("root", acme, platformAdmin: true);
        var root = await InDbAsync(db => db.Users.Where(u => u.UserName == "root").Select(u => u.Id).SingleAsync());
        using var admin = await AdminFor(acme, Permissions.UserManage + "," + Permissions.SessionManage);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/User/{eve}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/User/{eve}/unlock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/User/{eve}/sessions/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/User/{root}/reset-password", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsync($"/api/User/{root}/sessions/revoke", null)).StatusCode);
    }

    [Fact]
    public async Task An_administrator_sees_and_ends_a_users_sessions()
    {
        var (tenantId, _) = await TenantAsync();
        await CreateSignInUserAsync("ada", tenantId);
        var id = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        using var phone = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var laptop = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var admin = await AdminFor(tenantId, Permissions.SessionManage);

        var list = (await Read(await admin.GetAsync($"/api/User/{id}/sessions"))).GetProperty("data");
        Assert.Equal(2, list.GetArrayLength());
        Assert.All(list.EnumerateArray(), s => Assert.True(s.GetProperty("isOpen").GetBoolean()));

        var firstSession = list[0].GetProperty("id").GetInt64();
        var one = await admin.PostAsync($"/api/User/{id}/sessions/{firstSession}/revoke", null);
        Assert.Equal(1, (await Read(one)).GetProperty("data").GetInt32());
        Assert.Equal(1, await InDbAsync(db => db.UserSessions.CountAsync(s => s.RevokedAt == null)));

        var all = await admin.PostAsync($"/api/User/{id}/sessions/revoke", null);
        Assert.Equal(1, (await Read(all)).GetProperty("data").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/User/{id}/sessions/9999/revoke", null)).StatusCode);
    }

    [Fact]
    public async Task Sessions_need_their_own_permission()
    {
        var (tenantId, _) = await TenantAsync();
        await CreateSignInUserAsync("ada", tenantId);
        var id = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        using var userManager = await AdminFor(tenantId, Permissions.UserManage);

        Assert.Equal(HttpStatusCode.Forbidden, (await userManager.GetAsync($"/api/User/{id}/sessions")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await userManager.PostAsync($"/api/User/{id}/sessions/revoke", null)).StatusCode);
    }

    [Fact]
    public async Task A_user_sees_marks_and_ends_their_own_sessions()
    {
        await CreateSignInUserAsync("ada");
        using var phone = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var laptop = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var list = (await Read(await laptop.GetAsync("/api/Auth/sessions"))).GetProperty("data");
        var other = list.EnumerateArray().Single(s => !s.GetProperty("isCurrent").GetBoolean()).GetProperty("id").GetInt64();

        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(1, list.EnumerateArray().Count(s => s.GetProperty("isCurrent").GetBoolean()));
        Assert.Equal(HttpStatusCode.OK, (await laptop.PostAsync($"/api/Auth/sessions/{other}/revoke", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_user_cannot_end_somebody_elses_session()
    {
        await CreateSignInUserAsync("ada");
        await CreateSignInUserAsync("bob");
        using var bob = Bearer((await SignInAsync("bob")).GetProperty("accessToken").GetString()!);
        await SignInAsync("ada");
        var adaSession = await InDbAsync(db => db.UserSessions.Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s.Id, u.UserName })
            .Where(x => x.UserName == "ada").Select(x => x.Id).SingleAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await bob.PostAsync($"/api/Auth/sessions/{adaSession}/revoke", null)).StatusCode);
    }

    [Fact]
    public async Task Signing_out_everywhere_ends_every_session_including_this_one()
    {
        await CreateSignInUserAsync("ada");
        using var phone = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var laptop = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.OK, (await laptop.PostAsync("/api/Auth/logout-all", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/Auth/me")).StatusCode);
    }

    private async Task<(HttpClient Admin, long TargetId)> ImpersonationSetupAsync()
    {
        var (tenantId, companyId) = await TenantAsync();
        await CreateSignInUserAsync("boss", tenantId, companyId, membership: FSIdentity.Domain.Entities.Accounts.UserType.CompanyAdmin);
        await CreateSignInUserAsync("ada", tenantId, companyId);
        var boss = await InDbAsync(db => db.Users.Where(u => u.UserName == "boss").Select(u => u.Id).SingleAsync());
        var ada = await InDbAsync(db => db.Users.Where(u => u.UserName == "ada").Select(u => u.Id).SingleAsync());
        var roleId = await CreateRoleAsync("Admin");
        await PostIdAsync("/api/RolePermission", new { roleId, permissionId = await CreatePermissionAsync(Permissions.Impersonate) });
        await PostIdAsync("/api/UserCompanyRole", new { userId = boss, companyId, roleId });
        return (Bearer((await SignInAsync("boss")).GetProperty("accessToken").GetString()!), ada);
    }

    [Fact]
    public async Task An_administrator_acts_as_a_user_and_every_trace_names_both()
    {
        var (admin, adaId) = await ImpersonationSetupAsync();
        using var _ = admin;

        var response = await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = adaId, reason = " Support ticket 42 " });
        var data = (await Read(response)).GetProperty("data");
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(data.GetProperty("accessToken").GetString()).Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(adaId.ToString(), claims["UserId"]);
        Assert.Equal("ada", claims["UserName"]);
        Assert.True(claims.ContainsKey("ImpersonatorId"));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, data.GetProperty("refreshToken").ValueKind);
        Assert.Equal("ada", data.GetProperty("user").GetProperty("userName").GetString());

        using var asAda = Bearer(data.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await asAda.GetAsync("/api/Auth/me")).StatusCode);
        var session = await InDbAsync(db => db.UserSessions.SingleAsync(s => s.ImpersonatorUserId != null));
        Assert.Equal(adaId, session.UserId);
        Assert.InRange(session.ExpiresAt, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(16));
        Assert.Equal(long.Parse(claims["ImpersonatorId"]), session.ImpersonatorUserId);
        var audit = await InDbAsync(db => db.AuditLogs.SingleAsync(a => a.Action == "Impersonate"));
        Assert.Equal(adaId.ToString(), audit.EntityId);
        Assert.Contains("Support ticket 42", audit.NewValues);
    }

    [Fact]
    public async Task Changes_made_while_acting_are_attributed_to_the_user_and_to_the_administrator()
    {
        var (admin, adaId) = await ImpersonationSetupAsync();
        using var _ = admin;
        var bossId = await InDbAsync(db => db.Users.Where(u => u.UserName == "boss").Select(u => u.Id).SingleAsync());
        var companyId = await InDbAsync(db => db.UserCompanies.Select(m => m.CompanyId).FirstAsync());
        var geographer = await CreateRoleAsync("Geographer");
        await PostIdAsync("/api/RolePermission", new { roleId = geographer, permissionId = await CreatePermissionAsync(Permissions.GeographyManage) });
        await PostIdAsync("/api/UserCompanyRole", new { userId = adaId, companyId, roleId = geographer });
        var token = (await Read(await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = adaId })))
            .GetProperty("data").GetProperty("accessToken").GetString()!;
        using var asAda = Bearer(token);

        var created = await asAda.PostAsJsonAsync("/api/Country", new { name = "Turkey", code = "TR", formatCode = "x" });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var row = await InDbAsync(db => db.AuditLogs.SingleAsync(a => a.EntityName == "Country"));
        Assert.Equal(adaId, row.UserId);
        Assert.Equal(bossId, row.ImpersonatedBy);
        var sessions = (await Read(await asAda.GetAsync("/api/Auth/sessions"))).GetProperty("data");
        Assert.Contains(sessions.EnumerateArray(), s => s.GetProperty("impersonatorUserId").ValueKind != System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task While_acting_the_credentials_endpoints_and_further_impersonation_are_closed()
    {
        var (admin, adaId) = await ImpersonationSetupAsync();
        using var _ = admin;
        var token = (await Read(await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = adaId })))
            .GetProperty("data").GetProperty("accessToken").GetString()!;
        using var asAda = Bearer(token);

        var password = await asAda.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = Password, newPassword = "Brand-new-1" });
        var twoFactor = await asAda.PostAsync("/api/Auth/2fa/setup", null);
        var again = await asAda.PostAsJsonAsync("/api/Auth/impersonate", new { userId = 1 });
        var device = await asAda.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "the-admins-phone", platform = "ios" });

        Assert.Equal(HttpStatusCode.Forbidden, device.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, password.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, twoFactor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
    }

    [Fact]
    public async Task Impersonation_has_its_own_permission_and_limits()
    {
        var (admin, adaId) = await ImpersonationSetupAsync();
        using var _ = admin;
        using var nobody = ClientFor(tenantId: 1, userId: 5, permissions: "none");
        var bossId = await InDbAsync(db => db.Users.Where(u => u.UserName == "boss").Select(u => u.Id).SingleAsync());
        await CreateSignInUserAsync("off", 1);
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.UserName == "off")).IsActive = false;
            return await db.SaveChangesAsync();
        });
        var offId = await InDbAsync(db => db.Users.Where(u => u.UserName == "off").Select(u => u.Id).SingleAsync());

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsJsonAsync("/api/Auth/impersonate", new { userId = adaId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = bossId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = offId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = 9999 })).StatusCode);
    }

    [Fact]
    public async Task Only_a_platform_administrator_may_act_as_one_and_nobody_reaches_another_tenant()
    {
        var (admin, _) = await ImpersonationSetupAsync();
        using var _ = admin;
        var globex = await CreateTenantAsync("GLOBEX");
        await CreateSignInUserAsync("eve", globex);
        await CreateSignInUserAsync("root", null, platformAdmin: true);
        var eve = await InDbAsync(db => db.Users.Where(u => u.UserName == "eve").Select(u => u.Id).SingleAsync());
        var root = await InDbAsync(db => db.Users.Where(u => u.UserName == "root").Select(u => u.Id).SingleAsync());

        var tenantId = await InDbAsync(db => db.Users.Where(u => u.UserName == "boss").Select(u => u.IdentityTenantId!.Value).SingleAsync());
        await CreateSignInUserAsync("localroot", tenantId, platformAdmin: true);
        var localRoot = await InDbAsync(db => db.Users.Where(u => u.UserName == "localroot").Select(u => u.Id).SingleAsync());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = localRoot })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = eve })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync("/api/Auth/impersonate", new { userId = root })).StatusCode);

        using var platform = ClientFor(userId: 1000, superAdmin: true);
        Assert.Equal(HttpStatusCode.OK, (await platform.PostAsJsonAsync("/api/Auth/impersonate", new { userId = eve })).StatusCode);
    }
}
