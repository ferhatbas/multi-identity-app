using System.Text.Json;
using System.Net;
using System.Net.Http.Json;
using FSIdentity.Domain.Audit;
using FSIdentity.Identity.Applications;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class AuditLogApiTests : ApiTestBase
{
    private async Task<System.Text.Json.JsonElement> PagedAsync(HttpClient client, string query = "")
    {
        var response = await client.GetAsync($"/api/AuditLog/paged{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await Read(response)).GetProperty("data");
    }

    private static string[] Actions(System.Text.Json.JsonElement page)
        => page.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("action").GetString()!).ToArray();

    [Fact]
    public async Task Creating_a_record_through_the_api_leaves_a_Create_row_with_the_actor()
    {
        using var user = ClientFor(tenantId: 3, companyId: 4, userId: 7);
        var response = await user.PostAsJsonAsync("/api/Country", new { name = "Turkey", code = "TR", formatCode = "tr-TR" });
        var countryId = (await Read(response)).GetProperty("data").GetProperty("id").GetInt64();

        var page = await PagedAsync(Client, "?entityName=Country");
        var row = page.GetProperty("data")[0];

        Assert.Equal(1, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal("Create", row.GetProperty("action").GetString());
        Assert.Equal(countryId.ToString(), row.GetProperty("entityId").GetString());
        Assert.Equal(7, row.GetProperty("userId").GetInt64());
        Assert.Equal(3, row.GetProperty("auditTenantId").GetInt64());
        Assert.Equal(4, row.GetProperty("auditCompanyId").GetInt64());
        Assert.Contains("\"Code\":\"TR\"", row.GetProperty("newValues").GetString());
    }

    [Fact]
    public async Task A_correlation_id_sent_with_the_request_is_recorded()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/Country")
        {
            Content = JsonContent.Create(new { name = "Turkey", code = "TR", formatCode = "x" }),
        };
        request.Headers.Add("X-Correlation-ID", "trace-me-1");
        await Client.SendAsync(request);

        var row = (await PagedAsync(Client)).GetProperty("data")[0];

        Assert.Equal("trace-me-1", row.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task A_records_history_lists_its_changes_newest_first()
    {
        var id = await CreateCountryAsync("TR");
        await Client.PutAsJsonAsync($"/api/Country/{id}", new { name = "Turkiye", code = "TR", formatCode = "x" });
        await Client.DeleteAsync($"/api/Country/{id}");

        var response = await Client.GetAsync($"/api/AuditLog/entity/Country/{id}");
        var rows = (await Read(response)).GetProperty("data").EnumerateArray().ToList();

        Assert.Equal(["Delete", "Update", "Create"], rows.Select(r => r.GetProperty("action").GetString()));
        Assert.Contains("Name", rows[1].GetProperty("changedFields").GetString());
    }

    [Fact]
    public async Task A_failed_request_leaves_no_row()
    {
        await CreateCountryAsync("TR");
        var before = (await PagedAsync(Client)).GetProperty("totalRecords").GetInt32();

        await Client.PostAsJsonAsync("/api/Country", new { name = "Again", code = "TR", formatCode = "x" });

        Assert.Equal(before, (await PagedAsync(Client)).GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Machine_loaded_seed_data_can_be_kept_out_of_the_trail()
    {
        using (var scope = Factory.Services.CreateScope())
        using (AuditLogSuppression.Suppress())
            await scope.ServiceProvider.GetRequiredService<ApplicationSeeder>().SeedAsync();

        Assert.Equal(0, (await PagedAsync(Client)).GetProperty("totalRecords").GetInt32());

        using (var scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ApplicationSeeder>().SeedAsync();
        }
        Assert.Equal(0, (await PagedAsync(Client)).GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task A_tenants_user_reads_only_that_tenants_rows_and_a_super_admin_reads_all()
    {
        using var acme = ClientFor(tenantId: 1, userId: 10);
        using var globex = ClientFor(tenantId: 2, userId: 20);
        await acme.PostAsJsonAsync("/api/Country", new { name = "A", code = "AA", formatCode = "x" });
        await globex.PostAsJsonAsync("/api/Country", new { name = "B", code = "BB", formatCode = "x" });
        using var admin = ClientFor(superAdmin: true);

        var seenByAcme = await PagedAsync(acme);
        var seenByAdmin = await PagedAsync(admin);

        Assert.Equal(1, seenByAcme.GetProperty("totalRecords").GetInt32());
        Assert.Equal(10, seenByAcme.GetProperty("data")[0].GetProperty("userId").GetInt64());
        Assert.Equal(2, seenByAdmin.GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task A_super_admin_with_a_tenant_claim_still_reads_every_tenants_rows()
    {
        using var acme = ClientFor(tenantId: 1);
        using var globex = ClientFor(tenantId: 2);
        await acme.PostAsJsonAsync("/api/Country", new { name = "A", code = "AA", formatCode = "x" });
        await globex.PostAsJsonAsync("/api/Country", new { name = "B", code = "BB", formatCode = "x" });
        using var admin = ClientFor(tenantId: 1, superAdmin: true);

        Assert.Equal(2, (await PagedAsync(admin)).GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Another_tenants_row_is_404_by_id_and_empty_in_a_history()
    {
        using var acme = ClientFor(tenantId: 1);
        using var globex = ClientFor(tenantId: 2);
        var created = await acme.PostAsJsonAsync("/api/Country", new { name = "A", code = "AA", formatCode = "x" });
        var countryId = (await Read(created)).GetProperty("data").GetProperty("id").GetInt64();
        var rowId = (await PagedAsync(acme)).GetProperty("data")[0].GetProperty("id").GetInt64();

        Assert.Equal(HttpStatusCode.OK, (await acme.GetAsync($"/api/AuditLog/{rowId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await globex.GetAsync($"/api/AuditLog/{rowId}")).StatusCode);
        var history = (await Read(await globex.GetAsync($"/api/AuditLog/entity/Country/{countryId}"))).GetProperty("data");
        Assert.Empty(history.EnumerateArray());
    }

    [Fact]
    public async Task The_trail_cannot_be_written_through_the_api()
    {
        var post = await Client.PostAsJsonAsync("/api/AuditLog", new { entityName = "X" });
        var delete = await Client.DeleteAsync("/api/AuditLog/1");
        var put = await Client.PutAsJsonAsync("/api/AuditLog/1", new { entityName = "X" });

        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, put.StatusCode);
    }

    [Fact]
    public async Task Rows_can_be_filtered_by_action_user_and_text()
    {
        using var ada = ClientFor(userId: 1);
        using var grace = ClientFor(userId: 2);
        var id = (await Read(await ada.PostAsJsonAsync("/api/Country",
            new { name = "Turkey", code = "TR", formatCode = "x" }))).GetProperty("data").GetProperty("id").GetInt64();
        await grace.PutAsJsonAsync($"/api/Country/{id}", new { name = "Turkiye", code = "TR", formatCode = "x" });
        await ada.PostAsJsonAsync("/api/Language", new { code = "xx", name = "Xish" });

        Assert.Equal(["Update"], Actions(await PagedAsync(Client, "?action=Update")));
        Assert.Equal(2, (await PagedAsync(Client, "?userId=1")).GetProperty("totalRecords").GetInt32());
        Assert.Equal(1, (await PagedAsync(Client, "?entityName=Language")).GetProperty("totalRecords").GetInt32());
        Assert.Equal(1, (await PagedAsync(Client, "?search=Name")).GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task A_date_without_a_time_in_to_means_the_whole_day()
    {
        await CreateCountryAsync("TR");
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var tomorrow = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");

        Assert.Equal(1, (await PagedAsync(Client, $"?from={today}&to={today}")).GetProperty("totalRecords").GetInt32());
        Assert.Equal(0, (await PagedAsync(Client, $"?to={yesterday}")).GetProperty("totalRecords").GetInt32());
        Assert.Equal(0, (await PagedAsync(Client, $"?from={tomorrow}")).GetProperty("totalRecords").GetInt32());
    }

    [Fact]
    public async Task Paging_is_newest_first_and_bounded()
    {
        await CreateCountryAsync("AA");
        await CreateCountryAsync("BB");
        await CreateCountryAsync("CC");

        var page = await PagedAsync(Client, "?pageSize=2");
        var second = await PagedAsync(Client, "?pageSize=2&pageNumber=2");

        Assert.Equal(3, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal(2, page.GetProperty("totalPages").GetInt32());
        var firstIds = page.GetProperty("data").EnumerateArray().Select(r => r.GetProperty("id").GetInt64()).ToArray();
        Assert.True(firstIds[0] > firstIds[1]);
        Assert.True(second.GetProperty("data")[0].GetProperty("id").GetInt64() < firstIds[1]);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/AuditLog/paged?pageSize=501")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/AuditLog/paged?pageNumber=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/AuditLog/entity/Country/1?max=0")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_id_is_404_in_the_envelope()
    {
        var response = await Client.GetAsync("/api/AuditLog/99999");
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("NOT_FOUND", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_actions_list_names_only_the_actions_that_occur()
    {
        await Client.PostAsJsonAsync("/api/Language", new { code = "xx", name = "Xish" });

        var actions = (await (await Client.GetAsync("/api/AuditLog/actions")).Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("data").EnumerateArray().Select(a => a.GetString()).ToList();

        Assert.Contains("Create", actions);
        Assert.DoesNotContain("Restore", actions);
    }
}
