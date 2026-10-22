using System.Net.Http.Json;

namespace FSIdentity.Tests.Api;

public class TimestampTests : ApiTestBase
{
    [Fact]
    public async Task Dates_end_with_Z_after_create_and_after_reading_back()
    {
        var created = await Client.PostAsJsonAsync("/api/Country", new { name = "X", code = "ZZ", formatCode = "x" });
        var id = (await Read(created)).GetProperty("data").GetProperty("id").GetInt64();
        await Client.PutAsJsonAsync($"/api/Country/{id}", new { name = "Y", code = "ZZ", formatCode = "x" });

        var fromCreate = (await Read(created)).GetProperty("data");
        var fromRead = (await Read(await Client.GetAsync($"/api/Country/{id}"))).GetProperty("data");

        Assert.EndsWith("Z", fromCreate.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", fromRead.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", fromRead.GetProperty("lastModifiedAt").GetString());
    }

    [Fact]
    public async Task The_deleted_at_date_ends_with_Z_too()
    {
        var id = await CreateCountryAsync();
        await Client.DeleteAsync($"/api/Country/{id}");

        var deleted = (await Read(await Client.GetAsync("/api/Country/deleted"))).GetProperty("data")[0];

        Assert.EndsWith("Z", deleted.GetProperty("deletedAt").GetString());
    }

    [Fact]
    public async Task The_envelope_timestamp_ends_with_Z()
    {
        var body = await Read(await Client.GetAsync("/api/Country"));

        Assert.EndsWith("Z", body.GetProperty("timestamp").GetString());
    }
}
