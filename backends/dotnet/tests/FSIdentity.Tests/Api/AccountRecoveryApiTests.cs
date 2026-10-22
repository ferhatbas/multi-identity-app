using System.Net;
using System.Net.Http.Json;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class AccountRecoveryApiTests : ApiTestBase
{
    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private Task<HttpResponseMessage> ForgotAsync(string email)
        => Anonymous.PostAsJsonAsync("/api/Auth/forgot-password", new { email });

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword)
        => Anonymous.PostAsJsonAsync("/api/Auth/reset-password", new { token, newPassword });

    private Task<HttpResponseMessage> LoginAsync(string password)
        => Anonymous.PostAsJsonAsync("/api/Auth/login", new { userName = "ada", password });

    [Fact]
    public async Task Asking_for_a_reset_answers_the_same_for_a_known_and_an_unknown_address()
    {
        await CreateSignInUserAsync("ada");

        var known = await ForgotAsync("ada@example.test");
        var unknown = await ForgotAsync("nobody@example.test");

        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal((await Read(known)).GetProperty("message").GetString(), (await Read(unknown)).GetProperty("message").GetString());
        Assert.Equal(["ada@example.test"], Emails.Sent.Select(m => m.To));
    }

    [Fact]
    public async Task The_e_mail_holds_a_link_with_the_token_and_the_database_holds_only_its_hash()
    {
        await CreateSignInUserAsync("ada");
        await ForgotAsync("ada@example.test");

        var message = Emails.Sent.Single();
        var token = Emails.TokenFor("ada@example.test");
        var stored = await InDbAsync(db => db.PasswordResets.Select(r => r.TokenHash).SingleAsync());

        Assert.Contains("/auth/reset-password?token=", message.Text);
        Assert.Contains("/auth/reset-password?token=", message.Html);
        Assert.NotEqual(token, stored);
        Assert.DoesNotContain(token, stored);
    }

    [Fact]
    public async Task A_second_request_within_a_minute_sends_nothing_more_and_an_inactive_user_gets_nothing()
    {
        await CreateSignInUserAsync("ada");
        await ForgotAsync("ada@example.test");
        await ForgotAsync("ada@example.test");
        Assert.Single(Emails.Sent);

        await CreateSignInUserAsync("off");
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.UserName == "off")).IsActive = false;
            return await db.SaveChangesAsync();
        });
        await ForgotAsync("off@example.test");

        Assert.Single(Emails.Sent);
    }

    [Fact]
    public async Task A_malformed_address_is_a_validation_error()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await ForgotAsync("not-an-address")).StatusCode);
    }

    [Fact]
    public async Task The_token_sets_a_new_password_and_works_only_once()
    {
        await CreateSignInUserAsync("ada");
        await ForgotAsync("ada@example.test");
        var token = Emails.TokenFor("ada@example.test");

        var reset = await ResetAsync(token, "Brand-new-1");
        var again = await ResetAsync(token, "Another-new-2");

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("Brand-new-1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(Password)).StatusCode);
    }

    [Fact]
    public async Task A_reset_signs_everybody_out_lifts_a_lockout_and_tells_the_owner()
    {
        await CreateSignInUserAsync("ada");
        using var phone = Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);
        for (var i = 0; i < 5; i++)
            await LoginAsync("wrong-pass-1");
        Assert.Equal((HttpStatusCode)423, (await LoginAsync(Password)).StatusCode);

        await ForgotAsync("ada@example.test");
        await ResetAsync(Emails.TokenFor("ada@example.test"), "Brand-new-1");

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/Auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync("Brand-new-1")).StatusCode);
        Assert.Contains(Emails.Sent, m => m.Subject.Contains("password was changed"));
    }

    [Fact]
    public async Task A_weak_password_is_refused_and_the_link_stays_usable()
    {
        await CreateSignInUserAsync("ada");
        await ForgotAsync("ada@example.test");
        var token = Emails.TokenFor("ada@example.test");

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "short")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResetAsync(token, "Brand-new-1")).StatusCode);
    }

    [Fact]
    public async Task Unknown_expired_and_replaced_tokens_are_refused_the_same_way()
    {
        await CreateSignInUserAsync("ada");
        await ForgotAsync("ada@example.test");
        var first = Emails.TokenFor("ada@example.test");
        await InDbAsync(async db =>
        {
            foreach (var reset in db.PasswordResets)
                reset.CreatedAt = DateTime.UtcNow.AddMinutes(-5);
            return await db.SaveChangesAsync();
        });
        await ForgotAsync("ada@example.test");
        var second = Emails.TokenFor("ada@example.test");

        var unknown = await ResetAsync("nope", "Brand-new-1");
        var replaced = await ResetAsync(first, "Brand-new-1");
        await InDbAsync(async db =>
        {
            foreach (var reset in db.PasswordResets)
                reset.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });
        var expired = await ResetAsync(second, "Brand-new-1");

        foreach (var response in new[] { unknown, replaced, expired })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("The link is invalid or has expired", (await Read(response)).GetProperty("message").GetString());
        }
    }

    [Fact]
    public async Task A_reset_for_a_user_who_has_since_been_switched_off_is_refused()
    {
        await CreateSignInUserAsync("ada");
        await ForgotAsync("ada@example.test");
        var token = Emails.TokenFor("ada@example.test");
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync()).IsActive = false;
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await ResetAsync(token, "Brand-new-1")).StatusCode);
    }

    private async Task<HttpClient> SignedInAsync()
        => Bearer((await SignInAsync()).GetProperty("accessToken").GetString()!);

    [Fact]
    public async Task A_confirmation_link_confirms_the_address_once()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.OK, (await user.PostAsync("/api/Auth/send-confirmation", null)).StatusCode);
        var token = Emails.TokenFor("ada@example.test");

        var confirmed = await Anonymous.PostAsJsonAsync("/api/Auth/confirm-email", new { token });
        var again = await Anonymous.PostAsJsonAsync("/api/Auth/confirm-email", new { token });

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.True(await InDbAsync(db => db.Users.Select(u => u.EmailConfirmed).SingleAsync()));
    }

    [Fact]
    public async Task An_address_that_is_already_confirmed_cannot_be_confirmed_again()
    {
        await CreateSignInUserAsync("ada");
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync()).EmailConfirmed = true;
            return await db.SaveChangesAsync();
        });
        using var user = await SignedInAsync();

        Assert.Equal(HttpStatusCode.Conflict, (await user.PostAsync("/api/Auth/send-confirmation", null)).StatusCode);
    }

    [Fact]
    public async Task A_link_for_an_address_the_user_no_longer_has_proves_nothing()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();
        await user.PostAsync("/api/Auth/send-confirmation", null);
        var token = Emails.TokenFor("ada@example.test");
        await InDbAsync(async db =>
        {
            (await db.Users.SingleAsync()).Email = "changed@example.test";
            return await db.SaveChangesAsync();
        });

        var response = await Anonymous.PostAsJsonAsync("/api/Auth/confirm-email", new { token });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await InDbAsync(db => db.Users.Select(u => u.EmailConfirmed).SingleAsync()));
    }

    [Fact]
    public async Task Unknown_and_expired_confirmation_tokens_are_refused()
    {
        await CreateSignInUserAsync("ada");
        using var user = await SignedInAsync();
        await user.PostAsync("/api/Auth/send-confirmation", null);
        var token = Emails.TokenFor("ada@example.test");
        await InDbAsync(async db =>
        {
            foreach (var confirmation in db.EmailConfirmations)
                confirmation.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            return await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.BadRequest, (await Anonymous.PostAsJsonAsync("/api/Auth/confirm-email", new { token = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Anonymous.PostAsJsonAsync("/api/Auth/confirm-email", new { token })).StatusCode);
    }

    [Fact]
    public async Task Sending_a_confirmation_needs_a_signed_in_user()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.PostAsync("/api/Auth/send-confirmation", null)).StatusCode);
    }
}
