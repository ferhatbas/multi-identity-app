using System.Net.Http.Json;
using FSIdentity.Identity.Applications;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class ApplicationScopeServiceTests : ApiTestBase
{
    private async Task<T> AskAsync<T>(Func<IApplicationScopeService, Task<T>> question)
    {
        using var scope = Factory.Services.CreateScope();
        return await question(scope.ServiceProvider.GetRequiredService<IApplicationScopeService>());
    }

    private Task<long> CreateApplicationAsync(string code, int displayOrder = 0)
        => PostIdAsync("/api/ApplicationType", new { name = code, code, displayOrder });

    private Task<long> MapAsync(long companyTypeId, long applicationTypeId, bool isDefault = false)
        => PostIdAsync("/api/CompanyTypeApplicationType", new { companyTypeId, applicationTypeId, isDefault });

    private async Task<(long CompanyId, long TypeId)> CompanyOfTypeAsync(string typeCode = "CLINIC")
    {
        var tenantId = await CreateTenantAsync($"T-{typeCode}");
        var typeId = await CreateCompanyTypeAsync(typeCode);
        var companyId = await PostIdAsync("/api/Company",
            new { name = "HQ", code = "HQ", tenantId, companyTypeId = typeId });
        return (companyId, typeId);
    }

    [Fact]
    public async Task The_mapping_flagged_as_default_wins_over_display_order()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        await MapAsync(typeId, await CreateApplicationAsync("FIRST", displayOrder: 1));
        var chosen = await CreateApplicationAsync("SECOND", displayOrder: 2);
        await MapAsync(typeId, chosen, isDefault: true);

        Assert.Equal(chosen, await AskAsync(s => s.ResolveActiveAsync(companyId, null)));
    }

    [Fact]
    public async Task A_remembered_preference_is_kept_while_the_type_still_allows_it()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        await MapAsync(typeId, await CreateApplicationAsync("FIRST", 1), isDefault: true);
        var preferred = await CreateApplicationAsync("SECOND", 2);
        await MapAsync(typeId, preferred);

        Assert.Equal(preferred, await AskAsync(s => s.ResolveActiveAsync(companyId, preferred)));
    }

    [Fact]
    public async Task A_preference_the_type_does_not_allow_falls_back_to_the_default()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        var home = await CreateApplicationAsync("HOME");
        await MapAsync(typeId, home, isDefault: true);
        var stranger = await CreateApplicationAsync("STRANGER");

        Assert.Equal(home, await AskAsync(s => s.ResolveActiveAsync(companyId, stranger)));
    }

    [Fact]
    public async Task Without_a_default_the_application_named_like_the_company_type_wins()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync("DIALYSIS");
        await MapAsync(typeId, await CreateApplicationAsync("HOSPITAL", 1));
        var twin = await CreateApplicationAsync("DIALYSIS", 5);
        await MapAsync(typeId, twin);

        Assert.Equal(twin, await AskAsync(s => s.ResolveActiveAsync(companyId, null)));
    }

    [Fact]
    public async Task Management_is_never_the_fallback_while_something_else_is_allowed()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        await MapAsync(typeId, await CreateApplicationAsync("MANAGEMENT", displayOrder: 0));
        var clinical = await CreateApplicationAsync("HOSPITAL", displayOrder: 9);
        await MapAsync(typeId, clinical);

        Assert.Equal(clinical, await AskAsync(s => s.ResolveActiveAsync(companyId, null)));
    }

    [Fact]
    public async Task Management_is_the_answer_when_it_is_the_only_thing_left()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        var management = await CreateApplicationAsync("MANAGEMENT");
        await MapAsync(typeId, management);

        Assert.Equal(management, await AskAsync(s => s.ResolveActiveAsync(companyId, null)));
    }

    [Fact]
    public async Task A_remembered_management_is_not_kept_for_an_ordinary_company()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        var management = await CreateApplicationAsync("MANAGEMENT");
        await MapAsync(typeId, management);
        var clinical = await CreateApplicationAsync("HOSPITAL", 9);
        await MapAsync(typeId, clinical, isDefault: true);

        Assert.Equal(clinical, await AskAsync(s => s.ResolveActiveAsync(companyId, management)));
    }

    [Fact]
    public async Task A_remembered_management_is_kept_for_a_management_company()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync("MANAGEMENT");
        var management = await CreateApplicationAsync("MANAGEMENT");
        await MapAsync(typeId, management);
        await MapAsync(typeId, await CreateApplicationAsync("HOSPITAL", 9), isDefault: true);

        Assert.Equal(management, await AskAsync(s => s.ResolveActiveAsync(companyId, management)));
    }

    [Fact]
    public async Task A_company_with_no_type_or_no_mapping_has_no_application()
    {
        var tenantId = await CreateTenantAsync();
        var untyped = await CreateCompanyAsync(tenantId, "NOTYPE");
        var (unmapped, _) = await CompanyOfTypeAsync("EMPTY");

        Assert.Null(await AskAsync(s => s.ResolveActiveAsync(untyped, null)));
        Assert.Null(await AskAsync(s => s.ResolveActiveAsync(unmapped, null)));
        Assert.Null(await AskAsync(s => s.ResolveActiveAsync(99999, null)));
    }

    [Fact]
    public async Task An_inactive_or_deleted_application_is_not_allowed()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        var inactive = await CreateApplicationAsync("INACTIVE");
        var gone = await CreateApplicationAsync("GONE");
        var live = await CreateApplicationAsync("LIVE", 9);
        await MapAsync(typeId, inactive);
        var goneMapping = await MapAsync(typeId, gone);
        await MapAsync(typeId, live);
        await Client.PutAsJsonAsync($"/api/ApplicationType/{inactive}",
            new { name = "INACTIVE", code = "INACTIVE", isActive = false });
        await Client.DeleteAsync($"/api/CompanyTypeApplicationType/{goneMapping}");
        await Client.DeleteAsync($"/api/ApplicationType/{gone}");

        var allowed = await AskAsync(s => s.GetAllowedForCompanyAsync(companyId));

        Assert.Equal(["LIVE"], allowed.Select(a => a.Code));
    }

    [Fact]
    public async Task A_session_keeps_the_application_on_its_token_even_management()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        var management = await CreateApplicationAsync("MANAGEMENT");
        await MapAsync(typeId, management);
        await MapAsync(typeId, await CreateApplicationAsync("HOSPITAL", 9), isDefault: true);

        Assert.Equal(management, await AskAsync(s => s.ResolveForSessionAsync(companyId, management)));
    }

    [Fact]
    public async Task A_token_application_the_company_does_not_allow_falls_back_to_the_home_one()
    {
        var (companyId, typeId) = await CompanyOfTypeAsync();
        var home = await CreateApplicationAsync("HOME");
        await MapAsync(typeId, home, isDefault: true);
        var stranger = await CreateApplicationAsync("STRANGER");

        Assert.Equal(home, await AskAsync(s => s.ResolveForSessionAsync(companyId, stranger)));
    }

    private Task<long> CreateModuleAsync(string code, bool isCore = false)
        => PostIdAsync("/api/AppModule", new { name = code, code, isCore });

    private Task GrantAsync(long applicationTypeId, long appModuleId)
        => PostIdAsync("/api/ApplicationTypeModule", new { applicationTypeId, appModuleId });

    [Fact]
    public async Task An_application_grants_its_modules_plus_every_core_module()
    {
        var appId = await CreateApplicationAsync("HOSPITAL");
        await CreateModuleAsync("CORE", isCore: true);
        await GrantAsync(appId, await CreateModuleAsync("LAB"));
        await CreateModuleAsync("BILLING");

        var codes = await AskAsync(s => s.GetModuleCodesForApplicationAsync(appId));

        Assert.Equal(["CORE", "LAB"], codes.OrderBy(c => c));
    }

    [Fact]
    public async Task A_module_granted_to_another_application_is_not_granted()
    {
        var mine = await CreateApplicationAsync("MINE");
        var other = await CreateApplicationAsync("OTHER");
        await GrantAsync(other, await CreateModuleAsync("LAB"));

        Assert.Empty(await AskAsync(s => s.GetModuleCodesForApplicationAsync(mine)));
    }

    [Fact]
    public async Task Module_codes_are_compared_without_regard_to_case()
    {
        var appId = await CreateApplicationAsync("HOSPITAL");
        await GrantAsync(appId, await CreateModuleAsync("LAB"));

        var codes = await AskAsync(s => s.GetModuleCodesForApplicationAsync(appId));

        Assert.Contains("lab", codes);
    }

    [Fact]
    public async Task An_inactive_module_or_grant_is_not_granted()
    {
        var appId = await CreateApplicationAsync("HOSPITAL");
        var off = await CreateModuleAsync("OFF");
        await GrantAsync(appId, off);
        var paused = await CreateModuleAsync("PAUSED");
        var pausedGrant = await PostIdAsync("/api/ApplicationTypeModule",
            new { applicationTypeId = appId, appModuleId = paused });
        await GrantAsync(appId, await CreateModuleAsync("ON"));
        await Client.PutAsJsonAsync($"/api/AppModule/{off}", new { name = "OFF", code = "OFF", isActive = false });
        await Client.PutAsJsonAsync($"/api/ApplicationTypeModule/{pausedGrant}",
            new { applicationTypeId = appId, appModuleId = paused, isActive = false });

        var codes = await AskAsync(s => s.GetModuleCodesForApplicationAsync(appId));

        Assert.Equal(["ON"], codes.OrderBy(c => c));
    }

    [Fact]
    public async Task No_application_means_only_the_core_modules()
    {
        await CreateModuleAsync("CORE", isCore: true);
        await CreateModuleAsync("LAB");

        var codes = await AskAsync(s => s.GetModuleCodesForApplicationAsync(null));

        Assert.Equal(["CORE"], codes);
    }

    [Fact]
    public async Task Grants_count_as_configured_only_once_one_exists()
    {
        Assert.False(await AskAsync(s => s.AnyModuleGrantsConfiguredAsync()));

        await GrantAsync(await CreateApplicationAsync("HOSPITAL"), await CreateModuleAsync("LAB"));

        Assert.True(await AskAsync(s => s.AnyModuleGrantsConfiguredAsync()));
    }
}