using System.Net.Http.Json;
using System.Text.Json;

namespace FSIdentity.Tests.Api;

public class GeographyLookupTests : ApiTestBase
{
    private async Task<JsonElement[]> Options(string url)
    {
        var body = await Read(await Client.GetAsync(url));
        return body.GetProperty("data").EnumerateArray().ToArray();
    }

    [Fact]
    public async Task States_of_a_country_are_listed_by_code_and_the_code_is_case_insensitive()
    {
        await SeedAsync();

        var states = await Options("/api/geography/states?countryCode=tr");

        Assert.Equal(7, states.Length);
        Assert.Equal(
            new[] { "TR-AKD", "TR-DAN", "TR-EGE", "TR-GDA", "TR-ICA", "TR-KAR", "TR-MAR" },
            states.Select(s => s.GetProperty("code").GetString()).ToArray());
    }

    [Fact]
    public async Task States_without_a_country_code_lists_every_active_state()
    {
        await SeedAsync();

        Assert.Equal(9, (await Options("/api/geography/states")).Length);
    }

    [Fact]
    public async Task An_unknown_country_code_returns_nothing_instead_of_everything()
    {
        await SeedAsync();

        Assert.Empty(await Options("/api/geography/states?countryCode=ZZ"));
        Assert.Empty(await Options("/api/geography/cities?countryCode=ZZ"));
    }

    [Fact]
    public async Task Cities_of_a_country_are_listed_by_name()
    {
        await SeedAsync();

        var cities = await Options("/api/geography/cities?countryCode=TR");

        Assert.Equal(81, cities.Length);
        var names = cities.Select(c => c.GetProperty("name").GetString()!).ToArray();
        Assert.Equal(names.OrderBy(n => n, StringComparer.Ordinal).ToArray(), names);
    }

    [Theory]
    [InlineData("TR-MAR", 11)]
    [InlineData("TR-EGE", 8)]
    [InlineData("TR-AKD", 8)]
    [InlineData("TR-ICA", 13)]
    [InlineData("TR-KAR", 18)]
    [InlineData("TR-DAN", 14)]
    [InlineData("TR-GDA", 9)]
    public async Task Cities_of_a_state_are_its_provinces(string stateCode, int expected)
    {
        await SeedAsync();
        var states = await Options("/api/geography/states?countryCode=TR");
        var stateId = states.Single(s => s.GetProperty("code").GetString() == stateCode).GetProperty("id").GetInt64();

        var cities = await Options($"/api/geography/cities?stateId={stateId}");

        Assert.Equal(expected, cities.Length);
        Assert.All(cities, c => Assert.Equal(stateId, c.GetProperty("parentId").GetInt64()));
    }

    [Fact]
    public async Task A_region_knows_its_country_through_parent_id()
    {
        await SeedAsync();

        var states = await Options("/api/geography/states?countryCode=US");

        Assert.Equal(2, states.Length);
        Assert.Single(states.Select(s => s.GetProperty("parentId").GetInt64()).Distinct());
    }

    [Fact]
    public async Task Inactive_and_deleted_rows_are_not_offered()
    {
        var countryId = await CreateCountryAsync();
        var active = await CreateStateAsync(countryId, "A-1");
        var inactive = await CreateStateAsync(countryId, "A-2");
        var deleted = await CreateStateAsync(countryId, "A-3");
        await Client.PutAsJsonAsync($"/api/State/{inactive}",
            new { code = "A-2", name = "Inactive", countryId, isActive = false });
        await Client.DeleteAsync($"/api/State/{deleted}");

        var offered = await Options("/api/geography/states?countryCode=TR");

        Assert.Equal(new[] { active }, offered.Select(s => s.GetProperty("id").GetInt64()).ToArray());
    }
}