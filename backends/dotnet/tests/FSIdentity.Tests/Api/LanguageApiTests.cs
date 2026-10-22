using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FSIdentity.Tests.Api;

public class LanguageApiTests : ApiTestBase
{
    private Task<long> CreateLanguageAsync(string code, bool fallback = false)
        => PostIdAsync("/api/Language", new { code, name = $"{code} language", isFallback = fallback });

    private async Task<JsonElement> GetAsync(long id)
        => (await Read(await Client.GetAsync($"/api/Language/{id}"))).GetProperty("data");

    private static object Body(string code, bool fallback = false, bool? isActive = null) => new
    {
        code,
        name = $"{code} language",
        isFallback = fallback,
        isActive,
    };

    [Fact]
    public async Task Create_returns_201_and_lower_cases_the_code()
    {
        var response = await Client.PostAsJsonAsync("/api/Language",
            new { code = " TR ", name = "Turkish", nativeName = "Türkçe", isRtl = false, displayOrder = 2 });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("tr", data.GetProperty("code").GetString());
        Assert.Equal("Türkçe", data.GetProperty("nativeName").GetString());
        Assert.Equal(2, data.GetProperty("displayOrder").GetInt32());
    }

    [Fact]
    public async Task Create_with_a_taken_code_is_409_whatever_the_case()
    {
        await CreateLanguageAsync("tr");

        var response = await Client.PostAsJsonAsync("/api/Language", Body("TR"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Marking_a_new_fallback_moves_the_flag_from_the_old_one()
    {
        var english = await CreateLanguageAsync("en", fallback: true);
        var turkish = await CreateLanguageAsync("tr", fallback: true);

        Assert.False((await GetAsync(english)).GetProperty("isFallback").GetBoolean());
        Assert.True((await GetAsync(turkish)).GetProperty("isFallback").GetBoolean());
    }

    [Fact]
    public async Task Marking_an_existing_language_as_fallback_via_put_also_moves_the_flag()
    {
        var english = await CreateLanguageAsync("en", fallback: true);
        var turkish = await CreateLanguageAsync("tr");

        var response = await Client.PutAsJsonAsync($"/api/Language/{turkish}", Body("tr", fallback: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await GetAsync(english)).GetProperty("isFallback").GetBoolean());
        Assert.True((await GetAsync(turkish)).GetProperty("isFallback").GetBoolean());
    }

    [Fact]
    public async Task The_only_fallback_cannot_be_unmarked_deactivated_or_deleted()
    {
        var english = await CreateLanguageAsync("en", fallback: true);

        var unmark = await Client.PutAsJsonAsync($"/api/Language/{english}", Body("en", fallback: false));
        var deactivate = await Client.PutAsJsonAsync($"/api/Language/{english}", Body("en", fallback: true, isActive: false));
        var delete = await Client.DeleteAsync($"/api/Language/{english}");

        Assert.Equal(HttpStatusCode.Conflict, unmark.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, deactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.True((await GetAsync(english)).GetProperty("isFallback").GetBoolean());
    }

    [Fact]
    public async Task A_restored_language_does_not_take_the_fallback_back()
    {
        var english = await CreateLanguageAsync("en", fallback: true);
        var turkish = await CreateLanguageAsync("tr", fallback: true);
        await Client.DeleteAsync($"/api/Language/{english}");

        var restored = await Client.PostAsync($"/api/Language/{english}/restore", null);

        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False((await GetAsync(english)).GetProperty("isFallback").GetBoolean());
        Assert.True((await GetAsync(turkish)).GetProperty("isFallback").GetBoolean());
    }

    [Fact]
    public async Task A_language_can_be_deleted_and_its_code_reused_but_not_restored_over_the_new_one()
    {
        var oldId = await CreateLanguageAsync("de");
        await Client.DeleteAsync($"/api/Language/{oldId}");
        await CreateLanguageAsync("de");

        var restore = await Client.PostAsync($"/api/Language/{oldId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
    }

    [Fact]
    public async Task Create_without_required_fields_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/Language", new { });
        var fields = (await Read(response)).GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString()).Order().ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new[] { "code", "name" }, fields);
    }
}