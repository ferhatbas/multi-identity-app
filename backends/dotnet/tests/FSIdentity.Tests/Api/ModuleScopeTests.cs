using System.Net;
using System.Security.Claims;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Modules;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FSIdentity.Tests.Api;

public class ModuleScopeTests : ApiTestBase
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    private async Task<(long CompanyId, long ApplicationId)> SetUpAsync(string typeCode = "CLINIC", params string[] modules)
    {
        var tenantId = await CreateTenantAsync($"T-{typeCode}");
        var typeId = await CreateCompanyTypeAsync(typeCode);
        var companyId = await PostIdAsync("/api/Company", new { name = "HQ", code = "HQ", tenantId, companyTypeId = typeId });
        var applicationId = await PostIdAsync("/api/ApplicationType", new { name = typeCode, code = typeCode });
        await PostIdAsync("/api/CompanyTypeApplicationType",
            new { companyTypeId = typeId, applicationTypeId = applicationId, isDefault = true });

        foreach (var code in modules)
        {
            var moduleId = await PostIdAsync("/api/AppModule", new { name = code, code });
            await PostIdAsync("/api/ApplicationTypeModule", new { applicationTypeId = applicationId, appModuleId = moduleId });
        }

        return (companyId, applicationId);
    }

    private ModuleScopeAccessor AccessorFor(
        IServiceScope scope, long? companyId, long? applicationId = null,
        bool strict = false, int cacheSeconds = 0, bool superAdmin = false)
    {
        var claims = new List<Claim>();
        if (companyId is not null) claims.Add(new Claim(ClaimNames.ActiveCompanyId, companyId.ToString()!));
        if (applicationId is not null) claims.Add(new Claim(ClaimNames.ActiveApplicationTypeId, applicationId.ToString()!));
        if (superAdmin) claims.Add(new Claim(ClaimNames.IsSuperAdmin, "true"));

        var http = new DefaultHttpContext();
        if (claims.Count > 0)
            http.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        return new ModuleScopeAccessor(
            scope.ServiceProvider.GetRequiredService<IApplicationScopeService>(),
            new HttpContextAccessor { HttpContext = http },
            _cache,
            Options.Create(new ModuleScopeOptions { StrictMode = strict, CacheSeconds = cacheSeconds }));
    }

    [Fact]
    public async Task Everything_is_allowed_while_no_grants_exist()
    {
        using var scope = Factory.Services.CreateScope();

        Assert.True(await AccessorFor(scope, companyId: null).IsGrantedAsync("LAB"));
    }

    [Fact]
    public async Task A_module_the_application_grants_is_allowed_and_another_is_not()
    {
        var (companyId, _) = await SetUpAsync("CLINIC", "LAB");
        using var scope = Factory.Services.CreateScope();
        var accessor = AccessorFor(scope, companyId);

        Assert.True(await accessor.IsGrantedAsync("LAB"));
        Assert.True(await accessor.IsGrantedAsync("lab"));
        Assert.False(await accessor.IsGrantedAsync("BILLING"));
    }

    [Fact]
    public async Task A_super_administrator_does_not_bypass_the_module_gate()
    {
        var (companyId, _) = await SetUpAsync("CLINIC", "LAB");
        using var scope = Factory.Services.CreateScope();

        Assert.False(await AccessorFor(scope, companyId, superAdmin: true).IsGrantedAsync("BILLING"));
    }

    [Fact]
    public async Task A_caller_with_no_application_passes_unless_strict_mode_is_on()
    {
        await SetUpAsync("CLINIC", "LAB");
        using var scope = Factory.Services.CreateScope();

        Assert.True(await AccessorFor(scope, companyId: null, strict: false).IsGrantedAsync("LAB"));
        Assert.False(await AccessorFor(scope, companyId: null, strict: true).IsGrantedAsync("LAB"));
    }

    [Fact]
    public async Task Strict_mode_does_not_weaken_a_resolved_application()
    {
        var (companyId, _) = await SetUpAsync("CLINIC", "LAB");
        using var scope = Factory.Services.CreateScope();

        Assert.False(await AccessorFor(scope, companyId, strict: false).IsGrantedAsync("BILLING"));
        Assert.True(await AccessorFor(scope, companyId, strict: true).IsGrantedAsync("LAB"));
    }

    [Fact]
    public async Task The_module_set_is_cached_for_the_configured_time_and_not_at_all_for_zero()
    {
        var (companyId, applicationId) = await SetUpAsync("CLINIC", "LAB");
        using (var scope = Factory.Services.CreateScope())
            Assert.True(await AccessorFor(scope, companyId, cacheSeconds: 60).IsGrantedAsync("LAB"));

        var billing = await PostIdAsync("/api/AppModule", new { name = "BILLING", code = "BILLING" });
        await PostIdAsync("/api/ApplicationTypeModule", new { applicationTypeId = applicationId, appModuleId = billing });

        using var later = Factory.Services.CreateScope();
        Assert.False(await AccessorFor(later, companyId, cacheSeconds: 60).IsGrantedAsync("BILLING"));
        Assert.True(await AccessorFor(later, companyId, cacheSeconds: 0).IsGrantedAsync("BILLING"));
    }

    [Fact]
    public async Task Mine_lists_the_modules_of_the_callers_application_in_the_envelope()
    {
        var (companyId, applicationId) = await SetUpAsync("CLINIC", "LAB", "BILLING");
        using var user = ClientFor(companyId: companyId, applicationTypeId: applicationId);

        var response = await user.GetAsync("/api/ModuleScope/mine");
        var data = (await Read(response)).GetProperty("data");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(applicationId, data.GetProperty("applicationTypeId").GetInt64());
        Assert.True(data.GetProperty("configured").GetBoolean());
        Assert.Equal(["BILLING", "LAB"], data.GetProperty("modules").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task Mine_for_an_anonymous_caller_is_empty_and_says_whether_grants_exist()
    {
        var before = (await Read(await Client.GetAsync("/api/ModuleScope/mine"))).GetProperty("data");
        await SetUpAsync("CLINIC", "LAB");
        var after = (await Read(await Client.GetAsync("/api/ModuleScope/mine"))).GetProperty("data");

        Assert.False(before.GetProperty("configured").GetBoolean());
        Assert.True(after.GetProperty("configured").GetBoolean());
        Assert.Empty(after.GetProperty("modules").EnumerateArray());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, after.GetProperty("applicationTypeId").ValueKind);
    }

    private sealed class FakeModules(params string[] granted) : IModuleScopeAccessor
    {
        public Task<HashSet<string>> GetGrantedCodesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new HashSet<string>(granted, StringComparer.OrdinalIgnoreCase));

        public async Task<bool> IsGrantedAsync(string moduleCode, CancellationToken cancellationToken = default)
            => (await GetGrantedCodesAsync(cancellationToken)).Contains(moduleCode);
    }

    private static AuthorizationFilterContext Context(IModuleScopeAccessor? accessor, bool signedIn = true)
    {
        var services = new ServiceCollection();
        if (accessor is not null)
            services.AddSingleton(accessor);

        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        if (signedIn)
            http.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("any", "claim")], "test"));

        return new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    }

    [Fact]
    public async Task The_attribute_lets_a_granted_module_through()
    {
        await new RequireModuleAttribute("LAB").OnAuthorizationAsync(Context(new FakeModules("LAB")));
    }

    [Fact]
    public async Task The_attribute_lets_the_caller_through_when_any_named_module_is_granted()
    {
        await new RequireModuleAttribute("LAB", "BILLING").OnAuthorizationAsync(Context(new FakeModules("BILLING")));
    }

    [Fact]
    public async Task The_attribute_refuses_with_403_and_a_module_code_when_none_is_granted()
    {
        var attribute = new RequireModuleAttribute("LAB", "BILLING");

        var refused = await Assert.ThrowsAsync<ModuleNotGrantedException>(
            () => attribute.OnAuthorizationAsync(Context(new FakeModules("ADMIN"))));

        var envelope = ExceptionMapper.Map(refused);
        Assert.Equal("403", envelope.Code);
        Assert.Equal("MODULE_NOT_GRANTED", Assert.Single(envelope.Errors).Code);
        Assert.Contains("'LAB' or 'BILLING'", refused.Message);
    }

    [Fact]
    public async Task The_attribute_leaves_anonymous_endpoints_and_unwired_hosts_alone()
    {
        var attribute = new RequireModuleAttribute("LAB");

        await attribute.OnAuthorizationAsync(Context(new FakeModules(), signedIn: false));
        await attribute.OnAuthorizationAsync(Context(accessor: null));
    }

    [Fact]
    public void The_attribute_needs_at_least_one_module_code()
    {
        Assert.Throws<ArgumentException>(() => new RequireModuleAttribute());
    }
}