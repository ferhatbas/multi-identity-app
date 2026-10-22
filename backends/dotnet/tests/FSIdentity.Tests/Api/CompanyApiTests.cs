using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FSIdentity.Tests.Api;

public class CompanyApiTests : ApiTestBase
{
    private static object Body(string code = "HQ", long? tenantId = null, string name = "Head office") => new
    {
        name,
        code,
        tenantId,
    };

    [Fact]
    public async Task A_platform_administrator_must_say_which_tenant()
    {
        using var admin = ClientFor(superAdmin: true);

        var response = await admin.PostAsJsonAsync("/api/Company", Body());
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("tenantId is required when no tenant is active", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_platform_administrator_creates_a_company_in_the_named_tenant()
    {
        var tenantId = await CreateTenantAsync();
        using var admin = ClientFor(superAdmin: true);

        var response = await admin.PostAsJsonAsync("/api/Company", Body(tenantId: tenantId));
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(tenantId, data.GetProperty("tenantId").GetInt64());
        Assert.Equal("HQ", data.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_tenant_user_creates_inside_their_own_tenant_without_naming_it()
    {
        var tenantId = await CreateTenantAsync();
        using var user = ClientFor(tenantId: tenantId, userId: 5);

        var response = await user.PostAsJsonAsync("/api/Company", Body());
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(tenantId, data.GetProperty("tenantId").GetInt64());
        Assert.Equal(5, data.GetProperty("createdBy").GetInt64());
    }

    [Fact]
    public async Task A_tenant_user_may_name_their_own_tenant()
    {
        var tenantId = await CreateTenantAsync();
        using var user = ClientFor(tenantId: tenantId);

        var response = await user.PostAsJsonAsync("/api/Company", Body(tenantId: tenantId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_tenant_user_cannot_create_a_company_in_another_tenant()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        using var acmeUser = ClientFor(tenantId: acme);

        var response = await acmeUser.PostAsJsonAsync("/api/Company", Body(tenantId: globex));
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("FORBIDDEN", body.GetProperty("errors")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Naming_a_tenant_that_does_not_exist_is_400()
    {
        using var admin = ClientFor(superAdmin: true);

        var response = await admin.PostAsJsonAsync("/api/Company", Body(tenantId: 999));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Tenant 999 does not exist", (await Read(response)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_user_never_sees_another_tenants_companies()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        var acmeCompany = await CreateCompanyAsync(acme, "ACME-HQ");
        await CreateCompanyAsync(globex, "GLOBEX-HQ");
        using var globexUser = ClientFor(tenantId: globex);

        var list = await Read(await globexUser.GetAsync("/api/Company"));
        var paged = (await Read(await globexUser.GetAsync("/api/Company/paged"))).GetProperty("data");
        var byId = await globexUser.GetAsync($"/api/Company/{acmeCompany}");

        Assert.Equal(new[] { "GLOBEX-HQ" },
            list.GetProperty("data").EnumerateArray().Select(c => c.GetProperty("code").GetString()).ToArray());
        Assert.Equal(1, paged.GetProperty("totalRecords").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, byId.StatusCode);
    }

    [Fact]
    public async Task A_user_cannot_change_or_delete_another_tenants_company()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        var acmeCompany = await CreateCompanyAsync(acme, "ACME-HQ");
        await CreateCompanyAsync(globex, "GLOBEX-HQ");
        using var globexUser = ClientFor(tenantId: globex);

        var put = await globexUser.PutAsJsonAsync($"/api/Company/{acmeCompany}", Body("HACKED"));
        var delete = await globexUser.DeleteAsync($"/api/Company/{acmeCompany}");
        var restore = await globexUser.PostAsync($"/api/Company/{acmeCompany}/restore", null);

        Assert.Equal(HttpStatusCode.NotFound, put.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, restore.StatusCode);
        var still = (await Read(await Client.GetAsync($"/api/Company/{acmeCompany}"))).GetProperty("data");
        Assert.Equal("ACME-HQ", still.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Deleted_companies_of_another_tenant_stay_hidden_too()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        var acmeCompany = await CreateCompanyAsync(acme);
        await Client.DeleteAsync($"/api/Company/{acmeCompany}");
        using var globexUser = ClientFor(tenantId: globex);

        var deleted = await Read(await globexUser.GetAsync("/api/Company/deleted"));

        Assert.Equal(0, deleted.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task A_platform_administrator_sees_every_tenants_companies()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        await CreateCompanyAsync(acme, "ACME-HQ");
        await CreateCompanyAsync(globex, "GLOBEX-HQ");
        using var admin = ClientFor(superAdmin: true);

        var list = await Read(await admin.GetAsync("/api/Company"));

        Assert.Equal(2, list.GetProperty("data").GetArrayLength());
    }

    [Fact]
    public async Task The_same_code_may_exist_in_two_tenants_but_not_twice_in_one()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        await CreateCompanyAsync(acme, "HQ");
        await CreateCompanyAsync(globex, "HQ");

        var duplicate = await Client.PostAsJsonAsync("/api/Company", Body("hq", acme));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Country_and_company_type_must_exist()
    {
        var tenantId = await CreateTenantAsync();

        var country = await Client.PostAsJsonAsync("/api/Company",
            new { name = "X", code = "X", tenantId, countryId = 999 });
        var type = await Client.PostAsJsonAsync("/api/Company",
            new { name = "X", code = "X", tenantId, companyTypeId = 999 });

        Assert.Equal(HttpStatusCode.BadRequest, country.StatusCode);
        Assert.Equal("Country 999 does not exist", (await Read(country)).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, type.StatusCode);
        Assert.Equal("Company type 999 does not exist", (await Read(type)).GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_company_can_carry_its_address_country_type_and_language()
    {
        var tenantId = await CreateTenantAsync();
        var countryId = await CreateCountryAsync();
        var typeId = await CreateCompanyTypeAsync();

        var response = await Client.PostAsJsonAsync("/api/Company", new
        {
            name = "Head office", code = "HQ", tenantId,
            email = "info@acme.test", phoneNumber = "+90 212 000 00 00", website = "https://acme.test",
            address = "Main street 1", city = "İstanbul", state = "Marmara", zip = "34000",
            countryId, companyTypeId = typeId, languageCode = "TR",
        });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("İstanbul", data.GetProperty("city").GetString());
        Assert.Equal(countryId, data.GetProperty("countryId").GetInt64());
        Assert.Equal(typeId, data.GetProperty("companyTypeId").GetInt64());
        Assert.Equal("tr", data.GetProperty("languageCode").GetString());
    }

    [Fact]
    public async Task An_invalid_email_is_400_with_a_field_error()
    {
        var tenantId = await CreateTenantAsync();

        var response = await Client.PostAsJsonAsync("/api/Company",
            new { name = "X", code = "X", tenantId, email = "not-an-email" });
        var body = await Read(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("email", body.GetProperty("errors")[0].GetProperty("field").GetString());
    }

    [Fact]
    public async Task A_company_never_moves_to_another_tenant_through_put()
    {
        var acme = await CreateTenantAsync("ACME");
        var globex = await CreateTenantAsync("GLOBEX");
        var companyId = await CreateCompanyAsync(acme);

        var response = await Client.PutAsJsonAsync($"/api/Company/{companyId}",
            new { name = "Head office", code = "HQ", tenantId = globex });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(acme, (await Read(response)).GetProperty("data").GetProperty("tenantId").GetInt64());
    }

    [Fact]
    public async Task A_company_cannot_be_restored_while_its_tenant_is_deleted()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);
        await Client.DeleteAsync($"/api/Company/{companyId}");
        await Client.DeleteAsync($"/api/Tenant/{tenantId}");

        var blocked = await Client.PostAsync($"/api/Company/{companyId}/restore", null);
        await Client.PostAsync($"/api/Tenant/{tenantId}/restore", null);
        var allowed = await Client.PostAsync($"/api/Company/{companyId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task Read_has_exactly_the_documented_fields()
    {
        var tenantId = await CreateTenantAsync();
        var companyId = await CreateCompanyAsync(tenantId);

        var data = (await Read(await Client.GetAsync($"/api/Company/{companyId}"))).GetProperty("data");

        var names = data.EnumerateObject().Select(p => p.Name).Order().ToArray();
        Assert.Equal(new[]
        {
            "address", "address2", "city", "code", "companyTypeId", "countryId", "createdAt", "createdBy",
            "deletedAt", "deletedBy", "email", "id", "isActive", "isDeleted", "languageCode",
            "lastModifiedAt", "lastModifiedBy", "name", "phoneNumber", "state", "tenantId", "website", "zip",
        }, names);
    }
}
