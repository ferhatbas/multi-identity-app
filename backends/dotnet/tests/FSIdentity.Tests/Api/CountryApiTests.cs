using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FSIdentity.Tests.Api;

public class CountryApiTests : IDisposable
{
    private readonly AppFactory _factory = new();
    private readonly HttpClient _client;

    public CountryApiTests()
    {
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add(TestIdentityStartupFilter.SuperAdminHeader, "true");
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static object NewCountry(string code = "TR", string name = "Turkiye") => new
    {
        name,
        code,
        formatCode = "tr-TR",
    };

    private async Task<JsonElement> Read(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private async Task<long> CreateAsync(string code = "TR", string name = "Turkiye")
    {
        var response = await _client.PostAsJsonAsync("/api/Country", NewCountry(code, name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Read(response)).GetProperty("data").GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task Create_returns_201_in_the_envelope_with_a_normalised_code()
    {
        var response = await _client.PostAsJsonAsync("/api/Country", NewCountry(" tr "));
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("201", body.GetProperty("code").GetString());
        Assert.Equal("Country created successfully", body.GetProperty("message").GetString());
        Assert.Equal("TR", body.GetProperty("data").GetProperty("code").GetString());
        Assert.True(body.GetProperty("data").GetProperty("id").GetInt64() > 0);
        Assert.True(body.GetProperty("data").GetProperty("isActive").GetBoolean());
        Assert.NotNull(body.GetProperty("data").GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task Create_ignores_fields_the_dto_does_not_carry()
    {
        var response = await _client.PostAsJsonAsync("/api/Country", new
        {
            name = "Turkiye",
            code = "TR",
            formatCode = "tr-TR",
            id = 99,
            isDeleted = true,
            createdBy = 5,
            identityCompanyId = 7,
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(99, data.GetProperty("id").GetInt64());
        Assert.False(data.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("createdBy").ValueKind);
    }

    [Fact]
    public async Task Create_without_required_fields_is_400_with_a_field_error_for_each()
    {
        var response = await _client.PostAsJsonAsync("/api/Country", new { });
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("400", body.GetProperty("code").GetString());
        Assert.Equal(1, body.GetProperty("types").GetInt32());
        var fields = body.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString()).Order().ToArray();
        Assert.Equal(new[] { "code", "formatCode", "name" }, fields);
        Assert.All(body.GetProperty("errors").EnumerateArray(),
            e => Assert.Equal("VALIDATION_ERROR", e.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Create_with_a_too_long_name_is_400()
    {
        var response = await _client.PostAsJsonAsync("/api/Country", NewCountry(name: new string('x', 257)));
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("name", body.GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task Create_with_malformed_json_is_400_in_the_envelope()
    {
        var response = await _client.PostAsync("/api/Country",
            new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json"));
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("400", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_with_a_taken_code_is_409_whatever_the_case()
    {
        await CreateAsync("TR");

        var response = await _client.PostAsJsonAsync("/api/Country", NewCountry("tr", "Other"));
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("CONFLICT", body.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal("Country code 'TR' already exists", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task GetAll_on_an_empty_table_is_200_with_an_empty_list()
    {
        var response = await _client.GetAsync("/api/Country");
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("No Countries found", body.GetProperty("message").GetString());
        Assert.Equal(0, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetAll_returns_the_created_rows()
    {
        await CreateAsync("TR");
        await CreateAsync("DE", "Germany");

        var body = await Read(await _client.GetAsync("/api/Country"));

        Assert.Equal("Countries retrieved successfully", body.GetProperty("message").GetString());
        Assert.Equal(2, body.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task GetById_returns_the_row_and_the_success_envelope_carries_the_trace_id()
    {
        var id = await CreateAsync();

        var response = await _client.GetAsync($"/api/Country/{id}");
        var body = await Read(response);

        Assert.Equal("200", body.GetProperty("code").GetString());
        Assert.Equal("Turkiye", body.GetProperty("data").GetProperty("name").GetString());
        Assert.Equal(
            Assert.Single(response.Headers.GetValues("X-Request-ID")),
            body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task GetById_of_a_missing_row_is_404_in_the_envelope()
    {
        var response = await _client.GetAsync("/api/Country/12345");
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Country 12345 not found", body.GetProperty("message").GetString());
        Assert.Equal("NOT_FOUND", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Read_has_exactly_the_fields_the_existing_api_returns()
    {
        var id = await CreateAsync();

        var data = (await Read(await _client.GetAsync($"/api/Country/{id}"))).GetProperty("data");

        var names = data.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[]
        {
            "code", "createdAt", "createdBy", "currencyCode", "currencySymbol", "dateFormat",
            "dateTimeFormat", "deletedAt", "deletedBy", "formatCode", "id", "isActive", "isDeleted",
            "languageCode", "lastModifiedAt", "lastModifiedBy", "name", "nameGlobal", "priceFormat",
            "region", "timeZone",
        }, names);
    }

    [Fact]
    public async Task Put_replaces_the_record_and_clears_omitted_optional_fields()
    {
        var id = (await Read(await _client.PostAsJsonAsync("/api/Country", new
        {
            name = "Turkiye", code = "TR", formatCode = "tr-TR", currencyCode = "TRY", region = "Asia",
        }))).GetProperty("data").GetProperty("id").GetInt64();

        var response = await _client.PutAsJsonAsync($"/api/Country/{id}", new
        {
            name = "Türkiye", code = "TR", formatCode = "tr-TR",
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Türkiye", data.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("currencyCode").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("region").ValueKind);
        Assert.NotNull(data.GetProperty("lastModifiedAt").GetString());
    }

    [Fact]
    public async Task Put_without_isActive_keeps_it_and_with_isActive_changes_it()
    {
        var id = await CreateAsync();

        var kept = (await Read(await _client.PutAsJsonAsync($"/api/Country/{id}", NewCountry())))
            .GetProperty("data");
        var changed = (await Read(await _client.PutAsJsonAsync($"/api/Country/{id}", new
        {
            name = "Turkiye", code = "TR", formatCode = "tr-TR", isActive = false,
        }))).GetProperty("data");

        Assert.True(kept.GetProperty("isActive").GetBoolean());
        Assert.False(changed.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task Put_of_a_missing_row_is_404()
    {
        var response = await _client.PutAsJsonAsync("/api/Country/999", NewCountry());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_to_a_code_that_another_country_has_is_409_but_its_own_code_is_fine()
    {
        await CreateAsync("TR");
        var germany = await CreateAsync("DE", "Germany");

        var clash = await _client.PutAsJsonAsync($"/api/Country/{germany}", NewCountry("TR", "Germany"));
        var own = await _client.PutAsJsonAsync($"/api/Country/{germany}", NewCountry("DE", "Deutschland"));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
    }

    [Fact]
    public async Task Delete_hides_the_row_and_restore_brings_it_back()
    {
        var id = await CreateAsync();

        var deleted = await _client.DeleteAsync($"/api/Country/{id}");
        var deletedBody = await Read(deleted);
        var afterDelete = await _client.GetAsync($"/api/Country/{id}");
        var deletedList = await Read(await _client.GetAsync("/api/Country/deleted"));
        var restored = await _client.PostAsync($"/api/Country/{id}/restore", null);
        var afterRestore = await _client.GetAsync($"/api/Country/{id}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.True(deletedBody.GetProperty("data").GetBoolean());
        Assert.Equal("Country deleted successfully", deletedBody.GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        Assert.Equal(1, deletedList.GetProperty("data").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(HttpStatusCode.OK, afterRestore.StatusCode);
    }

    [Fact]
    public async Task Delete_of_a_missing_row_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync("/api/Country/999")).StatusCode);
    }

    [Fact]
    public async Task A_deleted_code_can_be_reused_but_then_the_old_row_cannot_be_restored()
    {
        var oldId = await CreateAsync("TR");
        await _client.DeleteAsync($"/api/Country/{oldId}");
        await CreateAsync("TR", "Replacement");

        var restore = await _client.PostAsync($"/api/Country/{oldId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
    }

    [Fact]
    public async Task Restore_of_a_row_that_is_not_deleted_is_404()
    {
        var id = await CreateAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync($"/api/Country/{id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Paged_returns_the_page_envelope_sorted_and_searched()
    {
        await CreateAsync("TR", "Turkiye");
        await CreateAsync("DE", "Germany");
        await CreateAsync("FR", "France");

        var page = (await Read(await _client.GetAsync(
            "/api/Country/paged?pageNumber=1&pageSize=2&orderBy=name&desc=true"))).GetProperty("data");
        var search = (await Read(await _client.GetAsync("/api/Country/paged?search=Ger"))).GetProperty("data");

        Assert.Equal(1, page.GetProperty("pageNumber").GetInt32());
        Assert.Equal(2, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        Assert.Equal(3, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal(new[] { "Turkiye", "Germany" },
            page.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("name").GetString()).ToArray());
        Assert.Equal(1, search.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Paged_uses_the_documented_defaults_when_no_parameters_are_sent()
    {
        await CreateAsync();

        var page = (await Read(await _client.GetAsync("/api/Country/paged"))).GetProperty("data");

        Assert.Equal(1, page.GetProperty("pageNumber").GetInt32());
        Assert.Equal(10, page.GetProperty("pageSize").GetInt32());
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=501")]
    [InlineData("pageNumber=0")]
    public async Task Paged_with_invalid_paging_is_400_in_the_envelope(string query)
    {
        var response = await _client.GetAsync($"/api/Country/paged?{query}");
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("400", body.GetProperty("code").GetString());
    }
}