using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FSIdentity.Identity.Auth;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FSIdentity.Tests.Api;

public class TotpTests
{
    private static readonly string RfcSecret = Base32.Encode(Encoding.ASCII.GetBytes("12345678901234567890"));

    [Fact]
    public void The_codes_match_the_published_test_vectors()
    {
        Assert.Equal("287082", Totp.Code(RfcSecret, 59 / 30));
        Assert.Equal("081804", Totp.Code(RfcSecret, 1111111109 / 30));
        Assert.Equal("050471", Totp.Code(RfcSecret, 1111111111 / 30));
        Assert.Equal("005924", Totp.Code(RfcSecret, 1234567890 / 30));
    }

    [Fact]
    public void Base32_round_trips_and_ignores_case_and_padding()
    {
        var data = RandomNumberGenerator.GetBytes(20);

        Assert.Equal(data, Base32.Decode(Base32.Encode(data)));
        Assert.Equal(data, Base32.Decode(Base32.Encode(data).ToLowerInvariant() + "===="));
        Assert.Throws<FormatException>(() => Base32.Decode("not*base32"));
    }

    [Fact]
    public void A_code_is_accepted_one_step_either_side_but_not_further()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000_000);
        var step = Totp.StepAt(now);

        Assert.Equal(step, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step), now, null));
        Assert.Equal(step - 1, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 1), now, null));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step + 1), now, null));
        Assert.Null(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 2), now, null));
        Assert.Null(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step + 2), now, null));
    }

    [Fact]
    public void A_step_that_was_used_is_never_accepted_again()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000_000);
        var step = Totp.StepAt(now);

        Assert.Null(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step), now, lastUsedStep: step));
        Assert.Null(Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step - 1), now, lastUsedStep: step));
        Assert.Equal(step + 1, Totp.Verify(RfcSecret, Totp.Code(RfcSecret, step + 1), now, lastUsedStep: step));
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("12345a")]
    public void Anything_but_six_digits_is_refused(string code)
    {
        Assert.Null(Totp.Verify(RfcSecret, code, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void The_authenticator_address_names_the_issuer_and_the_account()
    {
        var uri = Totp.Uri("FSIdentity", "ada@example.test", RfcSecret);

        Assert.StartsWith("otpauth://totp/FSIdentity:ada%40example.test?secret=", uri);
        Assert.Contains("&issuer=FSIdentity", uri);
        Assert.Contains("&digits=6&period=30", uri);
    }
}

public class SecretProtectorTests
{
    private static SecretProtector Make(string? dataKey = null, string jwtKey = "signing-key-signing-key-0123456789ab")
        => new(Options.Create(new SecurityOptions { DataKey = dataKey }), Options.Create(new JwtOptions { Key = jwtKey }));

    [Fact]
    public void A_secret_round_trips_and_the_ciphertext_hides_it()
    {
        var protector = Make();

        var protectedText = protector.Protect("JBSWY3DPEHPK3PXP");

        Assert.StartsWith("v1:", protectedText);
        Assert.DoesNotContain("JBSWY3DPEHPK3PXP", protectedText);
        Assert.Equal("JBSWY3DPEHPK3PXP", protector.Unprotect(protectedText));
    }

    [Fact]
    public void Protecting_twice_gives_different_text()
    {
        var protector = Make();

        Assert.NotEqual(protector.Protect("same"), protector.Protect("same"));
    }

    [Fact]
    public void A_changed_ciphertext_or_the_wrong_key_is_refused()
    {
        var text = Make().Protect("secret");
        var bytes = Convert.FromBase64String(text[3..]);
        bytes[^1] ^= 1;

        Assert.ThrowsAny<CryptographicException>(() => Make().Unprotect("v1:" + Convert.ToBase64String(bytes)));
        Assert.ThrowsAny<CryptographicException>(() => Make(jwtKey: "another-key-another-key-0123456789ab").Unprotect(text));
        Assert.ThrowsAny<CryptographicException>(() => Make().Unprotect("plain text"));
    }

    [Fact]
    public void A_separate_data_key_wins_over_the_signing_key()
    {
        var text = Make(dataKey: "data-key-data-key-data-key-0123456789").Protect("secret");

        Assert.Equal("secret", Make(dataKey: "data-key-data-key-data-key-0123456789", jwtKey: "completely-different-signing-key-12345").Unprotect(text));
    }
}

public class TwoFactorChallengeTokenTests
{
    private static (TokenService Tokens, FakeTimeProvider Clock) Make()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new JwtOptions { Key = "signing-key-signing-key-0123456789ab" });
        return (new TokenService(options, clock), clock);
    }

    [Fact]
    public async Task A_challenge_names_its_user_and_expires_after_five_minutes()
    {
        var (tokens, clock) = Make();
        var token = tokens.CreateTwoFactorToken(42);

        Assert.Equal(42, await tokens.ReadTwoFactorTokenAsync(token));

        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal(42, await tokens.ReadTwoFactorTokenAsync(token));

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Null(await tokens.ReadTwoFactorTokenAsync(token));
    }

    [Fact]
    public async Task Garbage_and_an_ordinary_access_token_are_not_challenges()
    {
        var (tokens, _) = Make();
        var user = new FSIdentity.Domain.Entities.Accounts.User { Id = 1, UserName = "ada" };
        var access = tokens.CreateAccessToken(user, null, null, 1).Value;

        Assert.Null(await tokens.ReadTwoFactorTokenAsync("not a token"));
        Assert.Null(await tokens.ReadTwoFactorTokenAsync(access));
    }
}

public class TwoFactorApiTests : ApiTestBase
{
    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<HttpClient> SignedInAsync()
        => Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

    private static string CodeFor(string secret, int stepsAhead = 0)
        => Totp.Code(secret, Totp.StepAt(DateTimeOffset.UtcNow) + stepsAhead);

    private async Task<(string Secret, string[] Recovery)> EnableAsync()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();
        var setup = (await Read(await user.PostAsync("/api/Auth/2fa/setup", null))).GetProperty("data");
        var secret = setup.GetProperty("secret").GetString()!;

        var enabled = await user.PostAsJsonAsync("/api/Auth/2fa/enable", new { code = CodeFor(secret) });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        var codes = (await Read(enabled)).GetProperty("data").GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        return (secret, codes);
    }

    private Task ForgetLastStepAsync() => InDbAsync(async db =>
    {
        (await db.Users.SingleAsync()).TwoFactorLastStep = null;
        return await db.SaveChangesAsync();
    });

    private Task<HttpResponseMessage> SecondStepAsync(string challenge, string code)
        => Anonymous.PostAsJsonAsync("/api/Auth/login/2fa", new { twoFactorToken = challenge, code });

    private async Task<string> ChallengeAsync()
    {
        var response = await Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName = "ada", password = Password });
        var data = (await Read(response)).GetProperty("data");
        Assert.True(data.GetProperty("twoFactorRequired").GetBoolean());
        return data.GetProperty("twoFactorToken").GetString()!;
    }

    [Fact]
    public async Task Setup_returns_a_seed_and_an_authenticator_address_and_stores_the_seed_encrypted()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();

        var data = (await Read(await user.PostAsync("/api/Auth/2fa/setup", null))).GetProperty("data");
        var secret = data.GetProperty("secret").GetString()!;
        var stored = await InDbAsync(db => db.Users.Select(u => u.TwoFactorSecret).SingleAsync());

        Assert.Equal(32, secret.Length);
        Assert.Contains($"secret={secret}", data.GetProperty("otpAuthUri").GetString());
        Assert.StartsWith("v1:", stored);
        Assert.DoesNotContain(secret, stored);
        Assert.False(await InDbAsync(db => db.Users.Select(u => u.TwoFactorEnabled).SingleAsync()));
    }

    [Fact]
    public async Task Enabling_needs_a_right_code_and_hands_out_ten_recovery_codes_once()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();
        var secret = (await Read(await user.PostAsync("/api/Auth/2fa/setup", null))).GetProperty("data").GetProperty("secret").GetString()!;

        var wrong = await user.PostAsJsonAsync("/api/Auth/2fa/enable", new { code = "000000" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.False(await InDbAsync(db => db.Users.Select(u => u.TwoFactorEnabled).SingleAsync()));

        var right = await user.PostAsJsonAsync("/api/Auth/2fa/enable", new { code = CodeFor(secret) });
        var codes = (await Read(right)).GetProperty("data").GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToList();

        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Equal(10, codes.Count);
        Assert.Equal(10, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches("^[a-z0-9]{5}-[a-z0-9]{5}$", c));
        Assert.Equal(10, await InDbAsync(db => db.UserRecoveryCodes.CountAsync()));
        var storedHashes = await InDbAsync(db => db.UserRecoveryCodes.Select(c => c.CodeHash).ToListAsync());
        Assert.All(codes, c => Assert.DoesNotContain(storedHashes, h => h.Contains(c)));
        Assert.Contains(Emails.Sent, m => m.Subject.Contains("Two-factor authentication is on"));
    }

    [Fact]
    public async Task Enabling_without_a_setup_or_twice_is_refused()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await user.PostAsJsonAsync("/api/Auth/2fa/enable", new { code = "123456" })).StatusCode);

        var (secret, _) = await EnableAsync2(user);
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsync("/api/Auth/2fa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsJsonAsync("/api/Auth/2fa/enable", new { code = CodeFor(secret, 1) })).StatusCode);
    }

    private async Task<(string Secret, string[] Recovery)> EnableAsync2(HttpClient user)
    {
        var secret = (await Read(await user.PostAsync("/api/Auth/2fa/setup", null))).GetProperty("data").GetProperty("secret").GetString()!;
        var enabled = await user.PostAsJsonAsync("/api/Auth/2fa/enable", new { code = CodeFor(secret) });
        var codes = (await Read(enabled)).GetProperty("data").GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();
        return (secret, codes);
    }

    [Fact]
    public async Task The_seed_and_the_codes_never_reach_the_audit_trail()
    {
        var (secret, codes) = await EnableAsync();

        var text = string.Join(' ', await InDbAsync(db => db.AuditLogs.Select(a => a.OldValues + a.NewValues + a.ChangedFields).ToListAsync()));

        var ciphertext = await InDbAsync(db => db.Users.Select(u => u.TwoFactorSecret).SingleAsync());
        Assert.DoesNotContain(secret, text);
        Assert.DoesNotContain(ciphertext!, text);
        Assert.All(codes, c => Assert.DoesNotContain(c, text));
        Assert.DoesNotContain("TwoFactorSecret", text);
        Assert.DoesNotContain("TwoFactorLastStep", text);
    }

    [Fact]
    public async Task With_2fa_on_the_password_alone_gives_a_challenge_and_no_tokens()
    {
        await EnableAsync();
        var sessionsBefore = await InDbAsync(db => db.UserSessions.CountAsync());

        var response = await Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName = "ada", password = Password });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(data.GetProperty("twoFactorRequired").GetBoolean());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("twoFactorToken").GetString()));
        Assert.True(string.IsNullOrEmpty(data.GetProperty("accessToken").GetString()));
        Assert.Equal(JsonValueKind.Null, data.GetProperty("refreshToken").ValueKind);
        Assert.Equal(sessionsBefore, await InDbAsync(db => db.UserSessions.CountAsync()));
    }

    [Fact]
    public async Task The_challenge_and_a_right_code_sign_the_user_in()
    {
        var (secret, _) = await EnableAsync();
        var challenge = await ChallengeAsync();

        var response = await SecondStepAsync(challenge, CodeFor(secret, 1));
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(data.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrEmpty(data.GetProperty("refreshToken").GetString()));
        using var user = Bearer(data.GetProperty("accessToken").GetString()!);
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/Auth/me")).StatusCode);
    }

    [Fact]
    public async Task A_code_works_once()
    {
        var (secret, _) = await EnableAsync();
        var challenge = await ChallengeAsync();
        var code = CodeFor(secret, 1);

        Assert.Equal(HttpStatusCode.OK, (await SecondStepAsync(challenge, code)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecondStepAsync(challenge, code)).StatusCode);
    }

    [Fact]
    public async Task The_challenge_cannot_be_used_as_a_bearer_token()
    {
        await EnableAsync();
        var challenge = await ChallengeAsync();
        using var thief = Bearer(challenge);

        Assert.Equal(HttpStatusCode.Unauthorized, (await thief.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await thief.GetAsync("/api/Country")).StatusCode);
    }

    [Fact]
    public async Task A_wrong_code_a_bad_challenge_and_a_foreign_token_are_refused()
    {
        var (secret, _) = await EnableAsync();
        var challenge = await ChallengeAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, (await SecondStepAsync(challenge, "000000")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecondStepAsync("not-a-challenge", CodeFor(secret, 1))).StatusCode);
        await CreateSignInUserAsync("bob");
        var access = (await SignInAsync("bob")).GetProperty("accessToken").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecondStepAsync(access, CodeFor(secret, 1))).StatusCode);
    }

    [Fact]
    public async Task Wrong_codes_count_towards_the_lockout()
    {
        await EnableAsync();
        var challenge = await ChallengeAsync();
        for (var i = 0; i < 5; i++)
            await SecondStepAsync(challenge, "000000");

        var locked = await SecondStepAsync(challenge, "000000");

        Assert.Equal((HttpStatusCode)423, locked.StatusCode);
        Assert.Equal((HttpStatusCode)423, (await Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName = "ada", password = Password })).StatusCode);
    }

    [Fact]
    public async Task A_recovery_code_signs_in_once_in_any_spelling()
    {
        var (_, recovery) = await EnableAsync();
        var code = recovery[0];

        var first = await SecondStepAsync(await ChallengeAsync(), code.ToUpperInvariant().Replace("-", " "));
        var second = await SecondStepAsync(await ChallengeAsync(), code);
        var other = await SecondStepAsync(await ChallengeAsync(), recovery[1]);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task New_recovery_codes_replace_the_old_ones_and_need_the_password_and_a_code()
    {
        var (secret, recovery) = await EnableAsync();
        using var user = await SignedInAsync2(secret);

        var wrongPassword = await user.PostAsJsonAsync("/api/Auth/2fa/recovery-codes", new { password = "nope-1234", code = recovery[0] });
        var fresh = await user.PostAsJsonAsync("/api/Auth/2fa/recovery-codes", new { password = Password, code = recovery[1] });
        var codes = (await Read(fresh)).GetProperty("data").GetProperty("codes").EnumerateArray().Select(c => c.GetString()!).ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(10, codes.Length);
        Assert.Empty(codes.Intersect(recovery));
        Assert.Equal(HttpStatusCode.Unauthorized, (await SecondStepAsync(await ChallengeAsync(), recovery[2])).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SecondStepAsync(await ChallengeAsync(), codes[0])).StatusCode);
    }

    private async Task<HttpClient> SignedInAsync2(string secret)
    {
        await ForgetLastStepAsync();
        var response = await SecondStepAsync(await ChallengeAsync(), CodeFor(secret));
        return Bearer((await Read(response)).GetProperty("data").GetProperty("accessToken").GetString()!);
    }

    [Fact]
    public async Task Turning_it_off_needs_the_password_and_a_code_and_removes_the_seed_and_codes()
    {
        var (secret, recovery) = await EnableAsync();
        using var user = await SignedInAsync2(secret);

        var noCode = await user.PostAsJsonAsync("/api/Auth/2fa/disable", new { password = Password, code = "000000" });
        var noPassword = await user.PostAsJsonAsync("/api/Auth/2fa/disable", new { password = "nope-1234", code = recovery[0] });
        Assert.Equal(HttpStatusCode.BadRequest, noCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPassword.StatusCode);
        Assert.True(await InDbAsync(db => db.Users.Select(u => u.TwoFactorEnabled).SingleAsync()));

        var off = await user.PostAsJsonAsync("/api/Auth/2fa/disable", new { password = Password, code = recovery[0] });

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False(await InDbAsync(db => db.Users.Select(u => u.TwoFactorEnabled).SingleAsync()));
        Assert.Null(await InDbAsync(db => db.Users.Select(u => u.TwoFactorSecret).SingleAsync()));
        Assert.Equal(0, await InDbAsync(db => db.UserRecoveryCodes.CountAsync()));
        Assert.Equal(HttpStatusCode.OK, (await Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName = "ada", password = Password })).StatusCode);
        Assert.Contains(Emails.Sent, m => m.Subject.Contains("Two-factor authentication is off"));
    }

    [Fact]
    public async Task Turning_it_off_when_it_is_not_on_is_a_conflict_and_all_of_it_needs_a_signed_in_user()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.Conflict,
            (await user.PostAsJsonAsync("/api/Auth/2fa/disable", new { password = Password, code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.PostAsync("/api/Auth/2fa/setup", null)).StatusCode);
    }
}
