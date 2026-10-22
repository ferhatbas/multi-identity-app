using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class AuthApiTests : ApiTestBase
{
    private async Task<(long TenantId, long CompanyId)> TenantWithCompanyAsync(string tenant = "ACME", string company = "HQ")
    {
        var tenantId = await CreateTenantAsync(tenant);
        return (tenantId, await CreateCompanyAsync(tenantId, company));
    }

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<HttpResponseMessage> LoginAsync(string userName, string password)
        => await Client.PostAsJsonAsync("/api/Auth/login", new { userName, password });

    [Fact]
    public async Task A_correct_password_returns_tokens_and_who_is_signed_in()
    {
        var (tenantId, companyId) = await TenantWithCompanyAsync();
        await CreateSignInUserAsync("ada", tenantId, companyId);

        var response = await LoginAsync("ada", Password);
        var body = await Read(response);
        var data = body.GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("refreshToken").GetString()));
        Assert.Equal("Bearer", data.GetProperty("tokenType").GetString());
        var user = data.GetProperty("user");
        Assert.Equal("ada", user.GetProperty("userName").GetString());
        Assert.Equal(tenantId, user.GetProperty("tenantId").GetInt64());
        Assert.Equal(companyId, user.GetProperty("activeCompanyId").GetInt64());
        Assert.Equal(companyId, user.GetProperty("companies")[0].GetProperty("companyId").GetInt64());
    }

    [Fact]
    public async Task The_response_never_contains_a_hash_or_secret()
    {
        await CreateSignInUserAsync("ada");

        var text = await (await LoginAsync("ada", Password)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("passwordHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("twoFactorSecret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$2", text);
    }

    [Fact]
    public async Task Signing_in_with_the_e_mail_address_works_too()
    {
        await CreateSignInUserAsync("ada");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ada@example.test", Password)).StatusCode);
    }

    [Fact]
    public async Task The_access_token_carries_the_contract_claims()
    {
        var (tenantId, companyId) = await TenantWithCompanyAsync();
        var applicationId = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        var typeId = await PostIdAsync("/api/CompanyType", new { name = "Clinic", code = "CLINIC" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = applicationId, isDefault = true });
        await Client.PutAsJsonAsync($"/api/Company/{companyId}", new { name = "HQ", code = "HQ", companyTypeId = typeId });
        var userId = await CreateSignInUserAsync("ada", tenantId, companyId);

        var token = (await SignInAsync()).GetProperty("accessToken").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(userId.ToString(), claims["UserId"]);
        Assert.Equal(tenantId.ToString(), claims["TenantId"]);
        Assert.Equal(companyId.ToString(), claims["ActiveCompanyId"]);
        Assert.Equal(applicationId.ToString(), claims["ActiveApplicationTypeId"]);
        Assert.Equal("false", claims["IsSuperAdmin"]);
        Assert.Equal("ada", claims["UserName"]);
        Assert.True(claims.ContainsKey("sid"));
        Assert.False(claims.ContainsKey("MustChangePassword"));
    }

    [Fact]
    public async Task A_platform_administrator_has_the_super_admin_claim_and_no_tenant()
    {
        await CreateSignInUserAsync("root", platformAdmin: true);

        var token = (await SignInAsync("root")).GetProperty("accessToken").GetString()!;
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal("true", claims["IsSuperAdmin"]);
        Assert.False(claims.ContainsKey("TenantId"));
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_user_are_refused_the_same_way()
    {
        await CreateSignInUserAsync("ada");

        var wrong = await LoginAsync("ada", "not-it-123");
        var unknown = await LoginAsync("nobody", "whatever1");
        var wrongBody = await Read(wrong);
        var unknownBody = await Read(unknown);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(wrongBody.GetProperty("message").GetString(), unknownBody.GetProperty("message").GetString());
        Assert.Equal("UNAUTHORIZED", wrongBody.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_inactive_user_cannot_sign_in()
    {
        await CreateSignInUserAsync("ada");
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync()).IsActive = false;
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("ada", Password)).StatusCode);
    }

    [Fact]
    public async Task A_missing_field_is_a_validation_error()
    {
        var response = await Client.PostAsJsonAsync("/api/Auth/login", new { userName = "ada" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Every_attempt_is_recorded_without_the_password()
    {
        await CreateSignInUserAsync("ada");
        await LoginAsync("ada", "bad-password-1");
        await LoginAsync("ada", Password);
        await LoginAsync("ghost", "whatever1");

        var attempts = await InDbAsync(db => db.LoginAttempts.OrderBy(a => a.Id).ToListAsync());

        Assert.Equal([false, true, false], attempts.Select(a => a.Succeeded));
        Assert.Equal(["WrongPassword", null, "UnknownUser"], attempts.Select(a => a.Reason));
        Assert.Null(attempts[2].UserId);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_against_the_right_password()
    {
        await CreateSignInUserAsync("ada");
        for (var i = 0; i < 5; i++)
            await LoginAsync("ada", "bad-password-1");

        var locked = await LoginAsync("ada", Password);
        var body = await Read(locked);

        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        Assert.Equal("ACCOUNT_LOCKED", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_success_in_between_resets_the_count()
    {
        await CreateSignInUserAsync("ada");
        for (var i = 0; i < 4; i++)
            await LoginAsync("ada", "bad-password-1");
        await LoginAsync("ada", Password);
        for (var i = 0; i < 4; i++)
            await LoginAsync("ada", "bad-password-1");

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ada", Password)).StatusCode);
    }

    [Fact]
    public async Task A_lockout_that_has_run_out_lets_the_user_in_again()
    {
        var userId = await CreateSignInUserAsync("ada");
        await InDbAsync(async db =>
        {
            db.UserLockouts.Add(new UserLockout
            {
                UserId = userId, LockedAt = DateTime.UtcNow.AddMinutes(-30), LockedUntil = DateTime.UtcNow.AddMinutes(-15),
            });
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ada", Password)).StatusCode);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user_and_the_modules_of_their_application()
    {
        var (tenantId, companyId) = await TenantWithCompanyAsync();
        await CreateSignInUserAsync("ada", tenantId, companyId);
        await PostIdAsync("/api/AppModule", new { name = "Core", code = "CORE", isCore = true });
        var token = (await SignInAsync()).GetProperty("accessToken").GetString()!;
        using var user = Bearer(token);

        var response = await user.GetAsync("/api/Auth/me");
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ada", data.GetProperty("user").GetProperty("userName").GetString());
        Assert.Equal(["CORE"], data.GetProperty("modules").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task Me_without_a_token_or_with_a_bad_one_is_401_in_the_envelope()
    {
        var anonymous = await Client.GetAsync("/api/Auth/me");
        using var forged = Bearer("not.a.token");
        var bad = await forged.GetAsync("/api/Auth/me");
        var body = await Read(anonymous);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("UNAUTHORIZED", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_token_signed_with_another_key_is_refused()
    {
        await CreateSignInUserAsync("ada");
        var token = (await SignInAsync()).GetProperty("accessToken").GetString()!;
        var tampered = token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA");
        using var user = Bearer(tampered);

        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_is_exchanged_once_for_a_new_pair()
    {
        await CreateSignInUserAsync("ada");
        var first = await SignInAsync();
        var refreshToken = first.GetProperty("refreshToken").GetString()!;

        var response = await Client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken });
        var next = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(refreshToken, next.GetProperty("refreshToken").GetString());
        using var user = Bearer(next.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task Using_a_refresh_token_twice_revokes_the_whole_session()
    {
        await CreateSignInUserAsync("ada");
        var refreshToken = (await SignInAsync()).GetProperty("refreshToken").GetString()!;
        var next = (await Read(await Client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken }))).GetProperty("data");

        var replay = await Client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken });
        var successor = await Client.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = next.GetProperty("refreshToken").GetString() });
        using var user = Bearer(next.GetProperty("accessToken").GetString()!);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, successor.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_or_expired_refresh_token_is_401()
    {
        await CreateSignInUserAsync("ada");
        var refreshToken = (await SignInAsync()).GetProperty("refreshToken").GetString()!;
        await InDbAsync(async db =>
        {
            foreach (var session in db.UserSessions)
                session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken })).StatusCode);
    }

    [Fact]
    public async Task Only_the_hash_of_a_refresh_token_is_stored()
    {
        await CreateSignInUserAsync("ada");
        var refreshToken = (await SignInAsync()).GetProperty("refreshToken").GetString()!;

        var stored = await InDbAsync(db => db.RefreshTokens.Select(t => t.TokenHash).SingleAsync());

        Assert.NotEqual(refreshToken, stored);
        Assert.DoesNotContain(refreshToken, stored);
    }

    [Fact]
    public async Task Refresh_stops_for_a_user_who_has_been_deactivated()
    {
        await CreateSignInUserAsync("ada");
        var refreshToken = (await SignInAsync()).GetProperty("refreshToken").GetString()!;
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync()).IsActive = false;
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.PostAsJsonAsync("/api/Auth/refresh", new { refreshToken })).StatusCode);
    }

    [Fact]
    public async Task Logging_out_ends_the_session_at_once()
    {
        await CreateSignInUserAsync("ada");
        var data = await SignInAsync();
        using var user = Bearer(data.GetProperty("accessToken").GetString()!);

        var logout = await user.PostAsync("/api/Auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await user.PostAsync("/api/Auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/Auth/refresh",
            new { refreshToken = data.GetProperty("refreshToken").GetString() })).StatusCode);
    }

    [Fact]
    public async Task A_member_can_change_company_and_a_stranger_cannot()
    {
        var (tenantId, first) = await TenantWithCompanyAsync();
        var second = await CreateCompanyAsync(tenantId, "BRANCH");
        var outsider = await CreateCompanyAsync(await CreateTenantAsync("OTHER"), "ELSE");
        await CreateSignInUserAsync("ada", tenantId, first);
        await InDbAsync(async db =>
        {
            db.UserCompanies.Add(new UserCompany { UserId = db.Users.Single().Id, CompanyId = second, IdentityTenantId = tenantId });
            return await db.SaveChangesAsync();
        });
        using var user = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var moved = await user.PostAsJsonAsync("/api/Auth/change-company", new { companyId = second });
        var refused = await user.PostAsJsonAsync("/api/Auth/change-company", new { companyId = outsider });
        var data = (await Read(moved)).GetProperty("data");
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(data.GetProperty("accessToken").GetString()).Claims
            .ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        Assert.Equal(second.ToString(), claims["ActiveCompanyId"]);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task A_platform_administrator_may_enter_any_company()
    {
        var (_, companyId) = await TenantWithCompanyAsync();
        await CreateSignInUserAsync("root", platformAdmin: true);
        using var admin = Bearer((await SignInAsync("root")).GetProperty("accessToken").GetString()!);

        var response = await admin.PostAsJsonAsync("/api/Auth/change-company", new { companyId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Changing_application_needs_one_the_companys_type_allows()
    {
        var (tenantId, companyId) = await TenantWithCompanyAsync();
        var typeId = await PostIdAsync("/api/CompanyType", new { name = "Clinic", code = "CLINIC" });
        await Client.PutAsJsonAsync($"/api/Company/{companyId}", new { name = "HQ", code = "HQ", companyTypeId = typeId });
        var home = await PostIdAsync("/api/ApplicationType", new { name = "Hospital", code = "HOSPITAL" });
        var other = await PostIdAsync("/api/ApplicationType", new { name = "Lab", code = "LAB" });
        var stranger = await PostIdAsync("/api/ApplicationType", new { name = "Other", code = "OTHER" });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = home, isDefault = true });
        await PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId = typeId, applicationTypeId = other });
        await CreateSignInUserAsync("ada", tenantId, companyId);
        using var user = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var allowed = await user.PostAsJsonAsync("/api/Auth/change-application", new { applicationTypeId = other });
        var refused = await user.PostAsJsonAsync("/api/Auth/change-application", new { applicationTypeId = stranger });
        var claims = new JwtSecurityTokenHandler()
            .ReadJwtToken((await Read(allowed)).GetProperty("data").GetProperty("accessToken").GetString()).Claims
            .ToDictionary(c => c.Type, c => c.Value);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(other.ToString(), claims["ActiveApplicationTypeId"]);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task The_language_comes_from_the_user_then_the_company_then_the_tenant_and_can_be_chosen_or_cleared()
    {
        await Client.PostAsJsonAsync("/api/Language", new { code = "en", name = "English", isFallback = true });
        await Client.PostAsJsonAsync("/api/Language", new { code = "tr", name = "Turkish", nativeName = "Türkçe" });
        await CreateSignInUserAsync("ada");
        using var user = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var before = (await (await user.GetAsync("/api/Auth/me")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("user");
        var chosen = await user.PutAsJsonAsync("/api/Auth/me/language", new { languageCode = "tr" });
        var unknown = await user.PutAsJsonAsync("/api/Auth/me/language", new { languageCode = "zz" });
        var after = (await (await user.GetAsync("/api/Auth/me")).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data").GetProperty("user");
        var cleared = (await (await user.PutAsJsonAsync("/api/Auth/me/language", new { languageCode = (string?)null })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");

        Assert.Equal("en", before.GetProperty("effectiveLanguageCode").GetString());
        Assert.Equal(HttpStatusCode.OK, chosen.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal("tr", after.GetProperty("effectiveLanguageCode").GetString());
        Assert.Equal("en", cleared.GetProperty("effectiveLanguageCode").GetString());
    }

    [Fact]
    public async Task Changing_the_password_checks_the_old_one_and_the_rules_for_the_new_one()
    {
        await CreateSignInUserAsync("ada");
        using var user = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var wrongCurrent = await user.PostAsJsonAsync("/api/Auth/change-password",
            new { currentPassword = "nope-1234", newPassword = "Brand-new-1" });
        var weak = await user.PostAsJsonAsync("/api/Auth/change-password",
            new { currentPassword = Password, newPassword = "short" });
        var same = await user.PostAsJsonAsync("/api/Auth/change-password",
            new { currentPassword = Password, newPassword = Password });

        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
    }

    [Fact]
    public async Task A_changed_password_works_at_once_and_signs_the_other_devices_out()
    {
        await CreateSignInUserAsync("ada");
        using var phone = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        using var laptop = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

        var changed = await laptop.PostAsJsonAsync("/api/Auth/change-password",
            new { currentPassword = Password, newPassword = "Brand-new-1" });

        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("ada", Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("ada", "Brand-new-1")).StatusCode);
    }

    [Fact]
    public async Task A_user_who_must_change_the_password_is_told_so_in_the_token_and_the_response()
    {
        await CreateSignInUserAsync("ada");
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync()).ShouldChangePasswordOnNextLogin = true;
            return await db.SaveChangesAsync();
        });

        var data = await SignInAsync();
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(data.GetProperty("accessToken").GetString()).Claims
            .ToDictionary(c => c.Type, c => c.Value);

        Assert.True(data.GetProperty("user").GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("true", claims["MustChangePassword"]);
    }

    [Fact]
    public async Task Sign_in_leaves_no_password_or_token_in_the_audit_trail()
    {
        await CreateSignInUserAsync("ada");
        var data = await SignInAsync();

        var text = string.Join(' ', await InDbAsync(db => db.AuditLogs.Select(a => a.OldValues + a.NewValues).ToListAsync()));

        Assert.DoesNotContain(data.GetProperty("refreshToken").GetString()!, text);
        Assert.DoesNotContain("$2", text);
    }
}
