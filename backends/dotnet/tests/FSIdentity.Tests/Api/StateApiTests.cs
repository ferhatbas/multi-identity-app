using System.Net;
using System.Net.Http.Json;

namespace FSIdentity.Tests.Api;

public class StateApiTests : ApiTestBase
{
    [Fact]
    public async Task Create_returns_201_and_normalises_the_code()
    {
        var countryId = await CreateCountryAsync();

        var response = await Client.PostAsJsonAsync("/api/State",
            new { code = " tr-mar ", name = "Marmara", countryId, description = "Bölge" });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("TR-MAR", data.GetProperty("code").GetString());
        Assert.Equal(countryId, data.GetProperty("countryId").GetInt64());
        Assert.Equal("Bölge", data.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Create_for_a_country_that_does_not_exist_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/State", new { code = "X", name = "X", countryId = 999 });
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Country 999 does not exist", body.GetProperty("message").GetString());
        Assert.Equal("BAD_REQUEST", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_without_a_country_id_is_400_with_a_field_error()
    {
        var response = await Client.PostAsJsonAsync("/api/State", new { code = "X", name = "X" });
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("countryId", body.GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task Create_with_a_taken_code_is_409()
    {
        var countryId = await CreateCountryAsync();
        await CreateStateAsync(countryId, "TR-MAR");

        var response = await Client.PostAsJsonAsync("/api/State", new { code = "tr-mar", name = "Other", countryId });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_cannot_move_a_state_to_a_country_that_does_not_exist()
    {
        var countryId = await CreateCountryAsync();
        var stateId = await CreateStateAsync(countryId);

        var response = await Client.PutAsJsonAsync($"/api/State/{stateId}",
            new { code = "TR-MAR", name = "Marmara", countryId = 999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_can_rename_a_state_and_keep_its_own_code()
    {
        var countryId = await CreateCountryAsync();
        var stateId = await CreateStateAsync(countryId);

        var response = await Client.PutAsJsonAsync($"/api/State/{stateId}",
            new { code = "TR-MAR", name = "Marmara Region", countryId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Marmara Region", (await Read(response)).GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_state_with_cities_cannot_be_deleted_until_they_are_gone()
    {
        var countryId = await CreateCountryAsync();
        var stateId = await CreateStateAsync(countryId);
        var cityId = await CreateCityAsync(stateId);

        var blocked = await Client.DeleteAsync($"/api/State/{stateId}");
        await Client.DeleteAsync($"/api/City/{cityId}");
        var allowed = await Client.DeleteAsync($"/api/State/{stateId}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task A_country_with_states_cannot_be_deleted_until_they_are_gone()
    {
        var countryId = await CreateCountryAsync();
        var stateId = await CreateStateAsync(countryId);

        var blocked = await Client.DeleteAsync($"/api/Country/{countryId}");
        await Client.DeleteAsync($"/api/State/{stateId}");
        var allowed = await Client.DeleteAsync($"/api/Country/{countryId}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task A_state_cannot_be_restored_while_its_country_is_deleted()
    {
        var countryId = await CreateCountryAsync();
        var stateId = await CreateStateAsync(countryId);
        await Client.DeleteAsync($"/api/State/{stateId}");
        await Client.DeleteAsync($"/api/Country/{countryId}");

        var blocked = await Client.PostAsync($"/api/State/{stateId}/restore", null);
        await Client.PostAsync($"/api/Country/{countryId}/restore", null);
        var allowed = await Client.PostAsync($"/api/State/{stateId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Paged_search_and_sort_work()
    {
        var countryId = await CreateCountryAsync();
        await CreateStateAsync(countryId, "TR-MAR");
        await CreateStateAsync(countryId, "TR-EGE");

        var page = (await Read(await Client.GetAsync("/api/State/paged?orderBy=code&search=TR-"))).GetProperty("data");

        Assert.Equal(2, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal("TR-EGE", page.GetProperty("data")[0].GetProperty("code").GetString());
    }
}