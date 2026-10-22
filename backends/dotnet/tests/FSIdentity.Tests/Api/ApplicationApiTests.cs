using System.Net;
using System.Net.Http.Json;

namespace FSIdentity.Tests.Api;

public class ApplicationApiTests : ApiTestBase
{
    private Task<long> CreateApplicationAsync(string code = "HOSPITAL")
        => PostIdAsync("/api/ApplicationType", new { name = $"{code} app", code });

    private Task<long> CreateModuleAsync(string code = "LAB", bool isCore = false)
        => PostIdAsync("/api/AppModule", new { name = $"{code} module", code, isCore });

    private async Task<HttpResponseMessage> MapAsync(long companyTypeId, long applicationTypeId, bool isDefault = false)
        => await Client.PostAsJsonAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId, applicationTypeId, isDefault });

    private async Task<bool> IsDefaultAsync(long mappingId)
    {
        var response = await Client.GetAsync($"/api/CompanyTypeApplicationType/{mappingId}");
        return (await Read(response)).GetProperty("data").GetProperty("isDefault").GetBoolean();
    }

    [Fact]
    public async Task An_application_code_is_trimmed_and_upper_cased()
    {
        var response = await Client.PostAsJsonAsync("/api/ApplicationType", new { name = "Hospital", code = " hospital " });
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("HOSPITAL", data.GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_taken_application_code_is_409()
    {
        await CreateApplicationAsync("HOSPITAL");

        var response = await Client.PostAsJsonAsync("/api/ApplicationType", new { name = "Other", code = "hospital" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_taken_module_code_is_409()
    {
        await CreateModuleAsync("LAB");

        var response = await Client.PostAsJsonAsync("/api/AppModule", new { name = "Other", code = "lab" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_mapping_needs_a_real_company_type_and_application()
    {
        var typeId = await CreateCompanyTypeAsync();
        var appId = await CreateApplicationAsync();

        var noType = await MapAsync(999, appId);
        var noApp = await MapAsync(typeId, 999);

        Assert.Equal(HttpStatusCode.BadRequest, noType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noApp.StatusCode);
    }

    [Fact]
    public async Task The_same_pair_cannot_be_mapped_twice()
    {
        var typeId = await CreateCompanyTypeAsync();
        var appId = await CreateApplicationAsync();
        await MapAsync(typeId, appId);

        var again = await MapAsync(typeId, appId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Marking_another_mapping_as_default_moves_the_flag()
    {
        var typeId = await CreateCompanyTypeAsync();
        var first = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = await CreateApplicationAsync("A"), isDefault = true });
        var second = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = await CreateApplicationAsync("B"), isDefault = true });

        Assert.False(await IsDefaultAsync(first));
        Assert.True(await IsDefaultAsync(second));
    }

    [Fact]
    public async Task Updating_a_mapping_to_default_moves_the_flag_too()
    {
        var typeId = await CreateCompanyTypeAsync();
        var appA = await CreateApplicationAsync("A");
        var first = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = appA, isDefault = true });
        var appB = await CreateApplicationAsync("B");
        var second = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = appB });

        var response = await Client.PutAsJsonAsync($"/api/CompanyTypeApplicationType/{second}",
            new { companyTypeId = typeId, applicationTypeId = appB, isDefault = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await IsDefaultAsync(first));
        Assert.True(await IsDefaultAsync(second));
    }

    [Fact]
    public async Task Different_company_types_each_have_their_own_default()
    {
        var appId = await CreateApplicationAsync();
        var a = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = await CreateCompanyTypeAsync("A"), applicationTypeId = appId, isDefault = true });
        var b = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = await CreateCompanyTypeAsync("B"), applicationTypeId = appId, isDefault = true });

        Assert.True(await IsDefaultAsync(a));
        Assert.True(await IsDefaultAsync(b));
    }

    [Fact]
    public async Task A_restored_mapping_does_not_take_the_default_back()
    {
        var typeId = await CreateCompanyTypeAsync();
        var first = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = await CreateApplicationAsync("A"), isDefault = true });
        await Client.DeleteAsync($"/api/CompanyTypeApplicationType/{first}");
        var second = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = await CreateApplicationAsync("B"), isDefault = true });

        var restored = await Client.PostAsync($"/api/CompanyTypeApplicationType/{first}/restore", null);

        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.False(await IsDefaultAsync(first));
        Assert.True(await IsDefaultAsync(second));
    }

    [Fact]
    public async Task A_company_type_with_an_application_mapping_cannot_be_deleted()
    {
        var typeId = await CreateCompanyTypeAsync();
        await MapAsync(typeId, await CreateApplicationAsync());

        var response = await Client.DeleteAsync($"/api/CompanyType/{typeId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task An_application_that_is_mapped_or_grants_modules_cannot_be_deleted()
    {
        var mapped = await CreateApplicationAsync("MAPPED");
        await MapAsync(await CreateCompanyTypeAsync(), mapped);
        var granting = await CreateApplicationAsync("GRANTING");
        await PostIdAsync("/api/ApplicationTypeModule",
            new { applicationTypeId = granting, appModuleId = await CreateModuleAsync() });

        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/ApplicationType/{mapped}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Client.DeleteAsync($"/api/ApplicationType/{granting}")).StatusCode);
    }

    [Fact]
    public async Task A_module_that_is_granted_cannot_be_deleted()
    {
        var moduleId = await CreateModuleAsync();
        await PostIdAsync("/api/ApplicationTypeModule",
            new { applicationTypeId = await CreateApplicationAsync(), appModuleId = moduleId });

        var response = await Client.DeleteAsync($"/api/AppModule/{moduleId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_mapping_cannot_come_back_under_a_deleted_application()
    {
        var appId = await CreateApplicationAsync();
        var mappingId = await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = await CreateCompanyTypeAsync(), applicationTypeId = appId });
        await Client.DeleteAsync($"/api/CompanyTypeApplicationType/{mappingId}");
        await Client.DeleteAsync($"/api/ApplicationType/{appId}");

        var response = await Client.PostAsync($"/api/CompanyTypeApplicationType/{mappingId}/restore", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_module_can_be_granted_to_an_application_only_once()
    {
        var appId = await CreateApplicationAsync();
        var moduleId = await CreateModuleAsync();
        await PostIdAsync("/api/ApplicationTypeModule", new { applicationTypeId = appId, appModuleId = moduleId });

        var again = await Client.PostAsJsonAsync("/api/ApplicationTypeModule",
            new { applicationTypeId = appId, appModuleId = moduleId });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A_grant_needs_a_real_application_and_module()
    {
        var appId = await CreateApplicationAsync();

        var response = await Client.PostAsJsonAsync("/api/ApplicationTypeModule",
            new { applicationTypeId = appId, appModuleId = 999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Applications_can_be_searched_and_sorted()
    {
        await CreateApplicationAsync("ALPHA");
        await CreateApplicationAsync("BRAVO");

        var response = await Client.GetAsync("/api/ApplicationType/paged?search=BRAV&orderBy=code&desc=true");
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(1, data.GetProperty("totalRecords").GetInt32());
        Assert.Equal("BRAVO", data.GetProperty("data")[0].GetProperty("code").GetString());
    }
}