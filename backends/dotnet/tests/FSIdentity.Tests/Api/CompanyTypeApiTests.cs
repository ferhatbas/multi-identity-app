using System.Net;
using System.Net.Http.Json;

namespace FSIdentity.Tests.Api;

public class CompanyTypeApiTests : ApiTestBase
{
    [Fact]
    public async Task Create_returns_201_and_normalises_the_code()
    {
        var response = await Client.PostAsJsonAsync("/api/CompanyType",
            new { name = "Clinic", code = " clinic ", description = "A clinic" });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("CLINIC", data.GetProperty("code").GetString());
        Assert.Equal("A clinic", data.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Create_with_a_code_longer_than_16_characters_is_400()
    {
        var response = await Client.PostAsJsonAsync("/api/CompanyType", new { name = "X", code = new string('A', 17) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_with_a_taken_code_is_409()
    {
        await CreateCompanyTypeAsync("CLINIC");

        var response = await Client.PostAsJsonAsync("/api/CompanyType", new { name = "Other", code = "clinic" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_unused_type_can_be_deleted_and_restored()
    {
        var id = await CreateCompanyTypeAsync();

        var deleted = await Client.DeleteAsync($"/api/CompanyType/{id}");
        var restored = await Client.PostAsync($"/api/CompanyType/{id}/restore", null);

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Fact]
    public async Task A_type_used_by_a_company_cannot_be_deleted_even_by_a_user_of_another_tenant()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        var typeId = await CreateCompanyTypeAsync();
        await PostIdAsync("/api/Company", new { name = "HQ", code = "HQ", tenantId = acme, companyTypeId = typeId });
        using var globexUser = ClientFor(tenantId: globex);

        var response = await globexUser.DeleteAsync($"/api/CompanyType/{typeId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Company_types_are_visible_to_every_tenant()
    {
        var acme = await CreateTenantAsync("ACME");
        await CreateTenantAsync("GLOBEX");
        await CreateCompanyTypeAsync();
        using var acmeUser = ClientFor(tenantId: acme);

        var body = await Read(await acmeUser.GetAsync("/api/CompanyType"));

        Assert.Equal(1, body.GetProperty("data").GetArrayLength());
    }
}
