using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Auth;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class SecurityApiTests : ApiTestBase
{
    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<EffectivePolicy> PolicyForAsync(long? tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISecurityPolicyService>().ForTenantAsync(tenantId);
    }

    private async Task<string> ErrorCodeAsync(HttpResponseMessage response)
        => (await Read(response)).GetProperty("errors")[0].GetProperty("code").GetString()!;

    private Task<long> PlatformPolicyAsync(object body) => PostIdAsync("/api/SecurityPolicy", body);

    private Task<long> TenantPolicyAsync(long tenantId, object fields)
    {
        var json = System.Text.Json.JsonSerializer.SerializeToElement(fields);
        var merged = json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        merged["tenantId"] = tenantId;
        return PostIdAsync("/api/SecurityPolicy", merged);
    }

    private Task<HttpResponseMessage> LoginAsync(string userName, string password)
        => Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName, password });

    [Fact]
    public async Task With_no_policy_the_built_in_settings_apply()
    {
        var policy = await PolicyForAsync(null);

        Assert.Equal(8, policy.MinPasswordLength);
        Assert.True(policy.PasswordRequiresLetter);
        Assert.True(policy.PasswordRequiresDigit);
        Assert.False(policy.PasswordRequiresSymbol);
        Assert.Equal(5, policy.MaxFailedAttempts);
        Assert.Equal(15, policy.LockoutMinutes);
        Assert.Equal(14, policy.SessionDays);
        Assert.Null(policy.RequireMfaFromRank);
        Assert.False(policy.RequireConfirmedEmail);
    }

    [Fact]
    public async Task The_platforms_policy_replaces_the_defaults_and_applies_to_every_tenant()
    {
        var tenantId = await CreateTenantAsync();
        await PlatformPolicyAsync(new { minPasswordLength = 12, maxFailedAttempts = 3, sessionDays = 2, requireConfirmedEmail = true });

        var policy = await PolicyForAsync(tenantId);

        Assert.Equal(12, policy.MinPasswordLength);
        Assert.Equal(3, policy.MaxFailedAttempts);
        Assert.Equal(2, policy.SessionDays);
        Assert.True(policy.RequireConfirmedEmail);
        Assert.Equal(15, policy.LockoutMinutes);
    }

    [Fact]
    public async Task A_tenant_can_make_things_stricter_but_never_looser()
    {
        var tenantId = await CreateTenantAsync();
        await PlatformPolicyAsync(new
        {
            minPasswordLength = 12, passwordRequiresSymbol = true, maxFailedAttempts = 3, lockoutMinutes = 30,
            sessionDays = 7, requireMfaFromRank = 2, requireConfirmedEmail = true, passwordMaxAgeDays = 90,
        });

        await TenantPolicyAsync(tenantId, new
        {
            minPasswordLength = 8, passwordRequiresSymbol = false, maxFailedAttempts = 10, lockoutMinutes = 5,
            sessionDays = 30, requireMfaFromRank = 3, requireConfirmedEmail = false, passwordMaxAgeDays = 365,
        });
        var looser = await PolicyForAsync(tenantId);
        Assert.Equal(12, looser.MinPasswordLength);
        Assert.True(looser.PasswordRequiresSymbol);
        Assert.Equal(3, looser.MaxFailedAttempts);
        Assert.Equal(30, looser.LockoutMinutes);
        Assert.Equal(7, looser.SessionDays);
        Assert.Equal(UserType.CompanyAdmin, looser.RequireMfaFromRank);
        Assert.True(looser.RequireConfirmedEmail);
        Assert.Equal(90, looser.PasswordMaxAgeDays);

        var other = await CreateTenantAsync("OTHER");
        await TenantPolicyAsync(other, new
        {
            minPasswordLength = 16, maxFailedAttempts = 2, lockoutMinutes = 60, sessionDays = 1,
            requireMfaFromRank = 1, passwordMaxAgeDays = 30, passwordRequiresMixedCase = true,
        });
        var stricter = await PolicyForAsync(other);
        Assert.Equal(16, stricter.MinPasswordLength);
        Assert.Equal(2, stricter.MaxFailedAttempts);
        Assert.Equal(60, stricter.LockoutMinutes);
        Assert.Equal(1, stricter.SessionDays);
        Assert.Equal(UserType.Standard, stricter.RequireMfaFromRank);
        Assert.Equal(30, stricter.PasswordMaxAgeDays);
        Assert.True(stricter.PasswordRequiresMixedCase);
    }

    [Fact]
    public async Task A_tenant_policy_that_says_nothing_about_a_rule_leaves_the_platforms_rule_in_force()
    {
        var tenantId = await CreateTenantAsync();
        await PlatformPolicyAsync(new { requireMfaFromRank = 2, requireConfirmedEmail = true, passwordMaxAgeDays = 60 });
        await TenantPolicyAsync(tenantId, new { minPasswordLength = 10 });

        var policy = await PolicyForAsync(tenantId);

        Assert.Equal(UserType.CompanyAdmin, policy.RequireMfaFromRank);
        Assert.True(policy.RequireConfirmedEmail);
        Assert.Equal(60, policy.PasswordMaxAgeDays);
        Assert.Equal(10, policy.MinPasswordLength);
    }

    [Fact]
    public async Task One_tenants_policy_does_not_touch_another_tenant()
    {
        var strict = await CreateTenantAsync("STRICT");
        var plain = await CreateTenantAsync("PLAIN");
        await TenantPolicyAsync(strict, new { minPasswordLength = 20 });

        Assert.Equal(20, (await PolicyForAsync(strict)).MinPasswordLength);
        Assert.Equal(8, (await PolicyForAsync(plain)).MinPasswordLength);
    }

    [Fact]
    public async Task An_inactive_policy_is_ignored()
    {
        var tenantId = await CreateTenantAsync();
        var id = await TenantPolicyAsync(tenantId, new { minPasswordLength = 20 });

        await Client.PutAsJsonAsync($"/api/SecurityPolicy/{id}", new { minPasswordLength = 20, tenantId, isActive = false });

        Assert.Equal(8, (await PolicyForAsync(tenantId)).MinPasswordLength);
    }

    [Fact]
    public async Task A_tenants_password_rules_apply_to_new_users()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { minPasswordLength = 14, passwordRequiresSymbol = true, passwordRequiresMixedCase = true });
        using var admin = ClientFor(tenantId: tenantId, userId: 1);

        object User(string name, string password) => new
        {
            userName = name, email = $"{name}@example.test", firstName = "A", lastName = "L", password,
        };

        var noSymbol = await admin.PostAsJsonAsync("/api/User", User("one", "Passw0rdPassw0rd"));
        var noMixedCase = await admin.PostAsJsonAsync("/api/User", User("two", "passw0rd!passw0rd"));
        var tooShort = await admin.PostAsJsonAsync("/api/User", User("three", "Short1!a"));
        var good = await admin.PostAsJsonAsync("/api/User", User("four", "Passw0rd!Passw0rd"));

        Assert.Equal(HttpStatusCode.BadRequest, noSymbol.StatusCode);
        Assert.Contains("a symbol", (await Read(noSymbol)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, noMixedCase.StatusCode);
        Assert.Contains("upper-case and a lower-case", (await Read(noMixedCase)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Contains("at least 14 characters", (await Read(tooShort)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
    }

    [Fact]
    public async Task A_symbol_is_demanded_when_the_policy_says_so()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { passwordRequiresSymbol = true });
        await CreateSignInUserAsync("ada", tenantId);
        using var user = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var response = await user.PostAsJsonAsync("/api/Auth/change-password", new { currentPassword = Password, newPassword = "Brandnew12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("a symbol", (await Read(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_generated_password_satisfies_even_the_strictest_policy()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new
        {
            minPasswordLength = 20, passwordRequiresSymbol = true, passwordRequiresMixedCase = true,
        });

        var response = await Client.PostAsJsonAsync("/api/User/import", new
        {
            tenantId, users = new[] { new { userName = "ada", email = "ada@example.test", firstName = "A", lastName = "L" } },
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(1, data.GetProperty("created").GetInt32());
    }

    [Fact]
    public async Task The_failure_limit_comes_from_the_tenants_policy()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { maxFailedAttempts = 2 });
        await CreateSignInUserAsync("ada", tenantId);

        await LoginAsync("ada", "wrong-pass-1");
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("ada", "wrong-pass-2")).StatusCode);

        Assert.Equal((HttpStatusCode)423, (await LoginAsync("ada", Password)).StatusCode);
    }

    [Fact]
    public async Task The_lockout_length_comes_from_the_policy()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { maxFailedAttempts = 1, lockoutMinutes = 120 });
        await CreateSignInUserAsync("ada", tenantId);

        await LoginAsync("ada", "wrong-pass-1");

        var lockout = await InDbAsync(db => db.UserLockouts.SingleAsync());
        Assert.InRange(lockout.LockedUntil - lockout.LockedAt, TimeSpan.FromMinutes(119), TimeSpan.FromMinutes(121));
    }

    [Fact]
    public async Task A_session_lasts_as_long_as_the_policy_says()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { sessionDays = 2 });
        await CreateSignInUserAsync("ada", tenantId);
        var refreshToken = (await SignInAsync()).GetProperty("refreshToken").GetString()!;

        var first = await InDbAsync(db => db.UserSessions.Select(s => s.ExpiresAt).SingleAsync());
        await Anonymous.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken });
        var afterRefresh = await InDbAsync(db => db.UserSessions.Select(s => s.ExpiresAt).SingleAsync());

        Assert.InRange(first, DateTime.UtcNow.AddDays(1.9), DateTime.UtcNow.AddDays(2.1));
        Assert.InRange(afterRefresh, DateTime.UtcNow.AddDays(1.9), DateTime.UtcNow.AddDays(2.1));
    }

    [Fact]
    public async Task An_old_password_must_be_changed_at_sign_in_and_a_fresh_one_does_not()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { passwordMaxAgeDays = 90 });
        await CreateSignInUserAsync("ada", tenantId);
        await CreateSignInUserAsync("bob", tenantId);
        await InDbAsync(async db =>
        {
            var ada = await db.Users.SingleAsync(u => u.UserName == "ada");
            ada.LastPasswordChangeAt = DateTime.UtcNow.AddDays(-100);
            var bob = await db.Users.SingleAsync(u => u.UserName == "bob");
            bob.LastPasswordChangeAt = DateTime.UtcNow.AddDays(-10);
            return await db.SaveChangesAsync();
        });

        var old = await SignInAsync("ada");
        var fresh = await SignInAsync("bob");
        using var user = Bearer(old.GetProperty("accessToken").GetString()!);

        Assert.True(old.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.False(fresh.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        var blocked = await user.GetAsync("/api/Country");
        Assert.Equal("PASSWORD_CHANGE_REQUIRED", await ErrorCodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, (await user.PostAsJsonAsync("/api/Auth/change-password",
            new { currentPassword = Password, newPassword = "Brand-new-1" })).StatusCode);
        Assert.False((await SignInAsync("ada", "Brand-new-1")).GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task A_rank_that_must_use_two_factor_is_held_to_setting_it_up_first()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);
        await TenantPolicyAsync(tenantId, new { requireMfaFromRank = 2 });
        await CreateSignInUserAsync("boss", tenantId, companyId, membership: UserType.CompanyAdmin);
        await CreateSignInUserAsync("staff", tenantId, companyId, membership: UserType.Standard);

        var boss = await SignInAsync("boss");
        var staff = await SignInAsync("staff");
        using var bossClient = Bearer(boss.GetProperty("accessToken").GetString()!);
        using var staffClient = Bearer(staff.GetProperty("accessToken").GetString()!);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(boss.GetProperty("accessToken").GetString()).Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.True(boss.GetProperty("user").GetProperty("mfaSetupRequired").GetBoolean());
        Assert.False(staff.GetProperty("user").GetProperty("mfaSetupRequired").GetBoolean());
        Assert.Equal("true", claims["MfaSetupRequired"]);
        var blocked = await bossClient.GetAsync("/api/Country");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("MFA_SETUP_REQUIRED", await ErrorCodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, (await bossClient.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await bossClient.PostAsync("/api/Auth/2fa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/Country")).StatusCode);
    }

    [Fact]
    public async Task Setting_up_two_factor_and_refreshing_lifts_the_hold()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);
        await TenantPolicyAsync(tenantId, new { requireMfaFromRank = 2 });
        await CreateSignInUserAsync("boss", tenantId, companyId, membership: UserType.CompanyAdmin);
        var signedIn = await SignInAsync("boss");
        using var boss = Bearer(signedIn.GetProperty("accessToken").GetString()!);
        var secret = (await Read(await boss.PostAsync("/api/Auth/2fa/setup", null))).GetProperty("data").GetProperty("secret").GetString()!;
        var code = Totp.Code(secret, Totp.StepAt(DateTimeOffset.UtcNow));

        Assert.Equal(HttpStatusCode.OK, (await boss.PostAsJsonAsync("/api/Auth/2fa/enable", new { code })).StatusCode);

        var refreshed = (await Read(await Anonymous.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = signedIn.GetProperty("refreshToken").GetString() }))).GetProperty("data");
        using var after = Bearer(refreshed.GetProperty("accessToken").GetString()!);
        Assert.False(refreshed.GetProperty("user").GetProperty("mfaSetupRequired").GetBoolean());
        Assert.NotEqual(HttpStatusCode.Forbidden, (await after.GetAsync("/api/Country")).StatusCode);
    }

    [Fact]
    public async Task A_platform_administrator_can_be_required_to_use_two_factor_too()
    {
        await PlatformPolicyAsync(new { requireMfaFromRank = 4 });
        await CreateSignInUserAsync("root", platformAdmin: true);

        var root = await SignInAsync("root");

        Assert.True(root.GetProperty("user").GetProperty("mfaSetupRequired").GetBoolean());
    }

    [Fact]
    public async Task Where_a_confirmed_address_is_required_the_user_must_confirm_it_first()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { requireConfirmedEmail = true });
        await CreateSignInUserAsync("ada", tenantId);
        var signedIn = await SignInAsync();
        using var user = Bearer(signedIn.GetProperty("accessToken").GetString()!);

        Assert.True(signedIn.GetProperty("user").GetProperty("emailConfirmationRequired").GetBoolean());
        var blocked = await user.GetAsync("/api/Country");
        Assert.Equal("EMAIL_CONFIRMATION_REQUIRED", await ErrorCodeAsync(blocked));

        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync("/api/Auth/send-confirmation", null)).StatusCode);
        var token = Emails.TokenFor("ada@example.test");
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.PostAsJsonAsync("/api/Auth/confirm-email", new { token })).StatusCode);

        var refreshed = (await Read(await Anonymous.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = signedIn.GetProperty("refreshToken").GetString() }))).GetProperty("data");
        Assert.False(refreshed.GetProperty("user").GetProperty("emailConfirmationRequired").GetBoolean());
        using var after = Bearer(refreshed.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await after.GetAsync("/api/Country")).StatusCode);
    }

    [Fact]
    public async Task An_impersonating_administrator_is_not_held_up_by_the_other_persons_restrictions()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { requireConfirmedEmail = true });
        await CreateSignInUserAsync("ada", tenantId);
        var adaId = await InDbAsync(db => db.Users.Select(u => u.Id).SingleAsync());
        using var platform = ClientFor(userId: 1000, superAdmin: true);

        var data = (await Read(await platform.PostAsJsonAsync("/api/Auth/impersonate", new { userId = adaId }))).GetProperty("data");

        Assert.False(data.GetProperty("user").GetProperty("emailConfirmationRequired").GetBoolean());
    }

    [Fact]
    public async Task A_tenant_administrator_sets_their_own_tenants_policy_once()
    {
        var tenantId = await CreateTenantAsync();
        using var admin = ClientFor(tenantId: tenantId, userId: 5, permissions: Permissions.SecurityManage);

        var created = await admin.PostAsJsonAsync("/api/SecurityPolicy", new { name = "Ours", minPasswordLength = 12 });
        var data = (await Read(created)).GetProperty("data");
        var again = await admin.PostAsJsonAsync("/api/SecurityPolicy", new { minPasswordLength = 14 });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(tenantId, data.GetProperty("tenantId").GetInt64());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(12, (await PolicyForAsync(tenantId)).MinPasswordLength);
    }

    [Fact]
    public async Task Only_a_platform_administrator_touches_the_platforms_own_policy_or_another_tenants()
    {
        var tenantId = await CreateTenantAsync();
        var other = await CreateTenantAsync("OTHER");
        var platformId = await PlatformPolicyAsync(new { minPasswordLength = 10 });
        using var admin = ClientFor(tenantId: tenantId, userId: 5, permissions: Permissions.SecurityManage);

        var foreign = await admin.PostAsJsonAsync("/api/SecurityPolicy", new { minPasswordLength = 12, tenantId = other });

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/SecurityPolicy/{platformId}", new { minPasswordLength = 6 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync($"/api/SecurityPolicy/{platformId}")).StatusCode);
    }

    [Fact]
    public async Task A_platform_administrator_with_no_tenant_active_may_not_make_a_policy_for_a_tenant_that_does_not_exist()
    {
        var missing = await Client.PostAsJsonAsync("/api/SecurityPolicy", new { minPasswordLength = 12, tenantId = 999 });

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Policy_values_are_range_checked_and_the_mfa_rank_must_be_a_real_one()
    {
        var tenantId = await CreateTenantAsync();
        foreach (var bad in new object[]
                 {
                     new { tenantId, minPasswordLength = 3 }, new { tenantId, maxFailedAttempts = 0 },
                     new { tenantId, sessionDays = 400 }, new { tenantId, requireMfaFromRank = 0 },
                 })
            Assert.Equal(HttpStatusCode.BadRequest, (await Client.PostAsJsonAsync("/api/SecurityPolicy", bad)).StatusCode);
    }

    [Fact]
    public async Task Policies_and_rules_need_the_security_permission()
    {
        using var nobody = ClientFor(tenantId: 1, userId: 5, permissions: "none");

        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.GetAsync("/api/SecurityPolicy")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await nobody.PostAsJsonAsync("/api/SoDRule", new { name = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await nobody.GetAsync("/api/Security/effective-policy")).StatusCode);
    }

    [Fact]
    public async Task Anyone_signed_in_can_read_the_policy_that_applies_to_them_and_their_own_status()
    {
        var tenantId = await CreateTenantAsync();
        await TenantPolicyAsync(tenantId, new { minPasswordLength = 15 });
        await CreateSignInUserAsync("ada", tenantId);
        using var user = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var policy = (await Read(await user.GetAsync("/api/Security/effective-policy"))).GetProperty("data");
        var status = (await Read(await user.GetAsync("/api/Security/status"))).GetProperty("data");

        Assert.Equal(15, policy.GetProperty("minPasswordLength").GetInt32());
        Assert.Equal(15, status.GetProperty("policy").GetProperty("minPasswordLength").GetInt32());
        Assert.False(status.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.GetAsync("/api/Security/status")).StatusCode);
    }
}
