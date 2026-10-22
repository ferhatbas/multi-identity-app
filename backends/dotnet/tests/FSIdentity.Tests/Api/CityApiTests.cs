using System.Net;
using System.Net.Http.Json;

namespace FSIdentity.Tests.Api;

public class CityApiTests : ApiTestBase
{
    private async Task<long> CreateStateInNewCountryAsync()
        => await CreateStateAsync(await CreateCountryAsync());

    [Fact]
    public async Task Create_returns_201_and_normalises_the_code()
    {
        var stateId = await CreateStateInNewCountryAsync();

        var response = await Client.PostAsJsonAsync("/api/City", new { code = " tr-34 ", name = "İstanbul", stateId });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("TR-34", data.GetProperty("code").GetString());
        Assert.Equal("İstanbul", data.GetProperty("name").GetString());
        Assert.Equal(stateId, data.GetProperty("stateId").GetInt64());
    }

    [Fact]
    public async Task Create_in_a_state_that_does_not_exist_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/City", new { code = "X", name = "X", stateId = 999 });
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("State 999 does not exist", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Create_with_a_taken_code_is_409()
    {
        var stateId = await CreateStateInNewCountryAsync();
        await CreateCityAsync(stateId, "TR-34");

        var response = await Client.PostAsJsonAsync("/api/City", new { code = "TR-34", name = "Other", stateId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_deleted_code_can_be_reused_but_then_the_old_city_cannot_be_restored()
    {
        var stateId = await CreateStateInNewCountryAsync();
        var oldId = await CreateCityAsync(stateId, "TR-34");
        await Client.DeleteAsync($"/api/City/{oldId}");
        await CreateCityAsync(stateId, "TR-34");

        var restore = await Client.PostAsync($"/api/City/{oldId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
    }

    [Fact]
    public async Task A_city_cannot_be_restored_while_its_state_is_deleted()
    {
        var stateId = await CreateStateInNewCountryAsync();
        var cityId = await CreateCityAsync(stateId);
        await Client.DeleteAsync($"/api/City/{cityId}");
        await Client.DeleteAsync($"/api/State/{stateId}");

        var blocked = await Client.PostAsync($"/api/City/{cityId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task Put_can_move_a_city_to_another_state()
    {
        var countryId = await CreateCountryAsync();
        var marmara = await CreateStateAsync(countryId, "TR-MAR");
        var aegean = await CreateStateAsync(countryId, "TR-EGE");
        var cityId = await CreateCityAsync(marmara);

        var response = await Client.PutAsJsonAsync($"/api/City/{cityId}",
            new { code = "TR-34", name = "Moved", stateId = aegean });

        Assert.Equal(aegean, (await Read(response)).GetProperty("data").GetProperty("stateId").GetInt64());
    }
}