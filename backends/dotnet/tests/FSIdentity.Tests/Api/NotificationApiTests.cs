using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FSIdentity.Domain.Entities.Notifications;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class NotificationApiTests : ApiTestBase
{
    private long _tenantId;

    private CapturedPushes Pushes => Factory.Services.GetRequiredService<CapturedPushes>();

    private async Task<T> InDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Factory.Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    private async Task<(HttpClient Client, long UserId)> PersonAsync(string userName = "ada")
    {
        if (_tenantId == 0)
            _tenantId = await CreateTenantAsync();

        var userId = await CreateUserAsync(userName, _tenantId);
        return (ClientFor(tenantId: _tenantId, userId: userId), userId);
    }

    private static JsonElement Find(JsonElement list, string category, string channel)
        => list.EnumerateArray().Single(p => p.GetProperty("category").GetString() == category && p.GetProperty("channel").GetString() == channel);

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response) => (await Read(response)).GetProperty("data");

    [Fact]
    public async Task Preferences_start_at_the_defaults_and_security_notices_cannot_be_turned_off()
    {
        var (ada, _) = await PersonAsync();

        var all = await DataAsync(await ada.GetAsync("/api/Notification/preferences"));

        Assert.Equal(8, all.GetArrayLength());
        Assert.True(Find(all, "ACTIVITY", "PUSH").GetProperty("enabled").GetBoolean());
        Assert.False(Find(all, "MARKETING", "PUSH").GetProperty("enabled").GetBoolean());
        Assert.True(Find(all, "MARKETING", "PUSH").GetProperty("isDefault").GetBoolean());
        Assert.True(Find(all, "SECURITY", "EMAIL").GetProperty("mandatory").GetBoolean());

        var off = await ada.PutAsJsonAsync("/api/Notification/preferences", new { items = new[] { new { category = "SECURITY", channel = "PUSH", enabled = false } } });
        Assert.Equal(HttpStatusCode.BadRequest, off.StatusCode);
    }

    [Fact]
    public async Task A_choice_is_saved_changed_and_checked()
    {
        var (ada, userId) = await PersonAsync();

        var saved = await DataAsync(await ada.PutAsJsonAsync("/api/Notification/preferences",
            new { items = new[] { new { category = "marketing", channel = "push", enabled = true }, new { category = "ACTIVITY", channel = "EMAIL", enabled = false } } }));
        Assert.True(Find(saved, "MARKETING", "PUSH").GetProperty("enabled").GetBoolean());
        Assert.False(Find(saved, "MARKETING", "PUSH").GetProperty("isDefault").GetBoolean());
        Assert.False(Find(saved, "ACTIVITY", "EMAIL").GetProperty("enabled").GetBoolean());

        await ada.PutAsJsonAsync("/api/Notification/preferences", new { items = new[] { new { category = "MARKETING", channel = "PUSH", enabled = false } } });
        Assert.Equal(2, await InDbAsync(db => db.UserNotificationPreferences.CountAsync(p => p.UserId == userId)));
        Assert.False(await InDbAsync(db => db.UserNotificationPreferences.Where(p => p.UserId == userId && p.Category == "MARKETING").Select(p => p.Enabled).SingleAsync()));

        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PutAsJsonAsync("/api/Notification/preferences",
            new { items = new[] { new { category = "GOSSIP", channel = "PUSH", enabled = true } } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PutAsJsonAsync("/api/Notification/preferences",
            new { items = new[] { new { category = "SYSTEM", channel = "SMOKE", enabled = true } } })).StatusCode);
    }

    [Fact]
    public async Task Preferences_are_private_to_the_person()
    {
        var (ada, _) = await PersonAsync();
        var (bob, _) = await PersonAsync("bob");

        await ada.PutAsJsonAsync("/api/Notification/preferences", new { items = new[] { new { category = "MARKETING", channel = "PUSH", enabled = true } } });

        Assert.False(Find(await DataAsync(await bob.GetAsync("/api/Notification/preferences")), "MARKETING", "PUSH").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task A_device_is_registered_once_and_its_token_is_never_returned()
    {
        var (ada, userId) = await PersonAsync();
        var body = new { token = "ExponentPushToken[AbC123]", platform = "ANDROID", deviceName = "Pixel" };

        var first = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-tokens", body));
        var again = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-tokens", body));

        Assert.Equal(first.GetProperty("id").GetInt64(), again.GetProperty("id").GetInt64());
        Assert.Equal("android", first.GetProperty("platform").GetString());
        Assert.False(first.TryGetProperty("token", out _));
        Assert.False(first.TryGetProperty("tokenHash", out _));

        var list = await ada.GetStringAsync("/api/Notification/push-tokens");
        Assert.DoesNotContain("AbC123", list);
        Assert.Equal(1, await InDbAsync(db => db.UserPushTokens.CountAsync(t => t.UserId == userId)));

        await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "exponentpushtoken[abc123]", platform = "ios" });
        Assert.Equal(2, await InDbAsync(db => db.UserPushTokens.CountAsync(t => t.UserId == userId)));
    }

    [Fact]
    public async Task A_device_that_changes_hands_changes_owner()
    {
        var (ada, _) = await PersonAsync();
        var (bob, bobId) = await PersonAsync("bob");
        var body = new { token = "shared-phone-token", platform = "ios" };

        await ada.PostAsJsonAsync("/api/Notification/push-tokens", body);
        await bob.PostAsJsonAsync("/api/Notification/push-tokens", body);

        Assert.Empty((await DataAsync(await ada.GetAsync("/api/Notification/push-tokens"))).EnumerateArray());
        Assert.Single((await DataAsync(await bob.GetAsync("/api/Notification/push-tokens"))).EnumerateArray());
        Assert.Equal(bobId, await InDbAsync(db => db.UserPushTokens.Select(t => t.UserId).SingleAsync()));
    }

    [Fact]
    public async Task Registration_is_checked_bounded_and_removal_is_the_owners_alone()
    {
        var (ada, _) = await PersonAsync();
        var (bob, _) = await PersonAsync("bob");

        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "abcdefgh", platform = "toaster" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "short", platform = "ios" })).StatusCode);

        for (var i = 0; i < 20; i++)
            Assert.Equal(HttpStatusCode.OK, (await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = $"device-token-{i}", platform = "web" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "device-token-21", platform = "web" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "device-token-3", platform = "web" })).StatusCode);

        var id = (await DataAsync(await ada.GetAsync("/api/Notification/push-tokens"))).EnumerateArray().First().GetProperty("id").GetInt64();
        Assert.False((await DataAsync(await bob.DeleteAsync($"/api/Notification/push-tokens/{id}"))).GetBoolean());
        Assert.True((await DataAsync(await ada.DeleteAsync($"/api/Notification/push-tokens/{id}"))).GetBoolean());
        Assert.Equal(19, (await DataAsync(await ada.GetAsync("/api/Notification/push-tokens"))).GetArrayLength());
    }

    [Fact]
    public async Task A_push_goes_to_every_device_and_leaves_a_ticket_for_each()
    {
        var (ada, userId) = await PersonAsync();
        await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "phone-token-1", platform = "android" });
        await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "tablet-token-1", platform = "ios" });

        var tickets = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "SYSTEM", title = "Hello", body = "World" }));

        Assert.Equal(2, tickets.GetArrayLength());
        Assert.All(tickets.EnumerateArray(), t => Assert.Equal("Sent", t.GetProperty("status").GetString()));
        Assert.Equal(["phone-token-1", "tablet-token-1"], Pushes.Sent.Select(m => m.Token).Order().ToArray());
        Assert.Equal(2, await InDbAsync(db => db.PushTickets.CountAsync(t => t.UserId == userId && t.Status == PushStatus.Sent)));
        Assert.Equal(2, (await DataAsync(await ada.GetAsync("/api/Notification/push-tickets"))).GetArrayLength());
    }

    [Fact]
    public async Task A_kind_the_person_turned_off_is_not_sent_but_is_recorded_as_skipped()
    {
        var (ada, _) = await PersonAsync();
        await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "phone-token-1", platform = "android" });
        await ada.PutAsJsonAsync("/api/Notification/preferences", new { items = new[] { new { category = "ACTIVITY", channel = "PUSH", enabled = false } } });

        var off = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "ACTIVITY", title = "t", body = "b" }));
        var marketingByDefault = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "MARKETING", title = "t", body = "b" }));

        Assert.Equal("Skipped", off[0].GetProperty("status").GetString());
        Assert.Equal("Skipped", marketingByDefault[0].GetProperty("status").GetString());
        Assert.Empty(Pushes.Sent);
    }

    [Fact]
    public async Task Security_notices_are_sent_whatever_the_person_chose()
    {
        var (ada, userId) = await PersonAsync();
        await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "phone-token-1", platform = "android" });
        await InDbAsync(async db =>
        {
            db.UserNotificationPreferences.Add(new UserNotificationPreference { UserId = userId, Category = "SECURITY", Channel = "PUSH", Enabled = false });
            return await db.SaveChangesAsync();
        });

        var result = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "security", title = "New sign-in", body = "From Berlin" }));

        Assert.Equal("Sent", result[0].GetProperty("status").GetString());
        Assert.Single(Pushes.Sent);
        Assert.True(Find(await DataAsync(await ada.GetAsync("/api/Notification/preferences")), "SECURITY", "PUSH").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task Nothing_is_sent_without_a_device_and_a_failing_provider_is_recorded_not_thrown()
    {
        var (ada, _) = await PersonAsync();

        var none = await DataAsync(await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "SYSTEM", title = "t", body = "b" }));
        Assert.Equal("Skipped", none[0].GetProperty("status").GetString());
        Assert.Equal("No registered device", none[0].GetProperty("error").GetString());

        await ada.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "phone-token-1", platform = "android" });
        Pushes.FailWith = "DeviceNotRegistered";
        var response = await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "SYSTEM", title = "t", body = "b" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var failed = await DataAsync(response);
        Assert.Equal("Failed", failed[0].GetProperty("status").GetString());
        Assert.Equal("DeviceNotRegistered", failed[0].GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, failed[0].GetProperty("sentAt").ValueKind);
    }

    [Fact]
    public async Task History_is_the_callers_own_and_bounded()
    {
        var (ada, _) = await PersonAsync();
        var (bob, _) = await PersonAsync("bob");

        for (var i = 0; i < 55; i++)
            await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "SYSTEM", title = $"n{i}", body = "b" });

        var mine = await DataAsync(await ada.GetAsync("/api/Notification/push-tickets"));
        Assert.Equal(50, mine.GetArrayLength());
        Assert.Equal("n54", mine[0].GetProperty("title").GetString());
        Assert.Empty((await DataAsync(await bob.GetAsync("/api/Notification/push-tickets"))).EnumerateArray());
    }

    [Fact]
    public async Task A_push_request_is_validated_and_the_endpoints_need_sign_in()
    {
        var (ada, _) = await PersonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "NOPE", title = "t", body = "b" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ada.PostAsJsonAsync("/api/Notification/push-test", new { category = "SYSTEM", title = "", body = "b" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.GetAsync("/api/Notification/preferences")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Anonymous.PostAsJsonAsync("/api/Notification/push-tokens", new { token = "abcdefgh", platform = "ios" })).StatusCode);
    }
}
