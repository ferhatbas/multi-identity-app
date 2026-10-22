using System.Net;
using System.Net.Http.Json;

namespace FSIdentity.Tests.Api;

public class TenantApiTests : ApiTestBase
{
    [Fact]
    public async Task Create_returns_201_and_normalises_code_and_language()
    {
        var response = await Client.PostAsJsonAsync("/api/Tenant",
            new { name = " Acme ", code = " acme ", languageCode = "TR" });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Acme", data.GetProperty("name").GetString());
        Assert.Equal("ACME", data.GetProperty("code").GetString());
        Assert.Equal("tr", data.GetProperty("languageCode").GetString());
    }

    [Fact]
    public async Task Create_without_required_fields_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/Tenant", new { });
        var fields = (await Read(response)).GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString()).Order().ToArray();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(new[] { "code", "name" }, fields);
    }

    [Fact]
    public async Task Create_with_a_taken_code_is_409_whatever_the_case()
    {
        await CreateTenantAsync("ACME");

        var response = await Client.PostAsJsonAsync("/api/Tenant", new { name = "Other", code = "acme" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_can_rename_a_tenant_and_keep_its_own_code()
    {
        var id = await CreateTenantAsync("ACME");

        var response = await Client.PutAsJsonAsync($"/api/Tenant/{id}", new { name = "Acme Corp", code = "ACME" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Acme Corp", (await Read(response)).GetProperty("data").GetProperty("name").GetString());
    }

    [Fact]
    public async Task A_tenant_with_companies_cannot_be_deleted_until_they_are_gone()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);

        var blocked = await Client.DeleteAsync($"/api/Tenant/{tenantId}");
        await Client.DeleteAsync($"/api/Company/{companyId}");
        var allowed = await Client.DeleteAsync($"/api/Tenant/{tenantId}");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task The_delete_check_sees_companies_even_when_the_caller_belongs_to_another_tenant()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        await CreateCompanyAsync(acme);
        using var globexUser = ClientFor(tenantId: globex);

        var response = await globexUser.DeleteAsync($"/api/Tenant/{acme}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_deleted_code_can_be_reused_but_then_the_old_tenant_cannot_be_restored()
    {
        var oldId = await CreateTenantAsync("ACME");
        await Client.DeleteAsync($"/api/Tenant/{oldId}");
        await CreateTenantAsync("ACME");

        var restore = await Client.PostAsync($"/api/Tenant/{oldId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, restore.StatusCode);
    }

    [Fact]
    public async Task Paged_search_and_sort_work()
    {
        await CreateTenantAsync("ACME");
        await CreateTenantAsync("GLOBEX");

        var page = (await Read(await Client.GetAsync("/api/Tenant/paged?orderBy=code&desc=true&search=tenant")))
            .GetProperty("data");

        Assert.Equal(2, page.GetProperty("totalRecords").GetInt32());
        Assert.Equal("GLOBEX", page.GetProperty("data")[0].GetProperty("code").GetString());
    }
}
