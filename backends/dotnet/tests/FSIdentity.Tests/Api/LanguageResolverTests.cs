using System.Net.Http.Json;
using FSIdentity.Identity.Localization;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Tests.Api;

public class LanguageResolverTests : ApiTestBase
{
    private async Task<string> ResolveAsync(long? userId, long? companyId, long? tenantId)
    {
        using var scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ILanguageResolver>().ResolveAsync(userId, companyId, tenantId);
    }

    private async Task LanguagesAsync(params (string Code, bool Fallback)[] languages)
    {
        foreach (var (code, fallback) in languages)
            await PostIdAsync("/api/Language", new { code, name = code, isFallback = fallback });
    }

    private async Task<(long TenantId, long CompanyId, long UserId)> WorldAsync(string? tenant, string? company, string? user)
    {
        var tenantId = await PostIdAsync("/api/Tenant", new { name = "T", code = "T", languageCode = tenant });
        var companyId = await PostIdAsync("/api/Company", new { name = "C", code = "C", tenantId, languageCode = company });
        var userId = await CreateSignInUserAsync("ada", tenantId, companyId);
        if (user is not null)
            await Client.PutAsJsonAsync($"/api/User/{userId}", new { userName = "ada", email = "ada@example.test", firstName = "A", lastName = "L", languageCode = user });
        return (tenantId, companyId, userId);
    }

    [Fact]
    public async Task The_persons_own_language_beats_the_companys_and_the_companys_beats_the_tenants()
    {
        await LanguagesAsync(("en", true), ("tr", false), ("de", false), ("fr", false));
        var (tenantId, companyId, userId) = await WorldAsync(tenant: "fr", company: "de", user: "tr");

        Assert.Equal("tr", await ResolveAsync(userId, companyId, tenantId));
        Assert.Equal("de", await ResolveAsync(null, companyId, tenantId));
        Assert.Equal("fr", await ResolveAsync(null, null, tenantId));
        Assert.Equal("en", await ResolveAsync(null, null, null));
    }

    [Fact]
    public async Task A_language_that_is_unknown_or_switched_off_is_skipped()
    {
        await LanguagesAsync(("en", true), ("tr", false), ("de", false));
        var (tenantId, companyId, userId) = await WorldAsync(tenant: "tr", company: "de", user: "xx");
        var german = await Client.GetAsync("/api/Language");
        var deId = (await Read(german)).GetProperty("data").EnumerateArray().Single(l => l.GetProperty("code").GetString() == "de").GetProperty("id").GetInt64();
        await Client.PutAsJsonAsync($"/api/Language/{deId}", new { code = "de", name = "de", isActive = false });

        Assert.Equal("tr", await ResolveAsync(userId, companyId, tenantId));
    }

    [Fact]
    public async Task Without_any_language_configured_it_is_english()
    {
        Assert.Equal("en", await ResolveAsync(null, null, null));
        Assert.Equal("en", await ResolveAsync(99, 99, 99));
    }

    [Fact]
    public async Task The_fallback_language_is_what_is_left()
    {
        await LanguagesAsync(("en", false), ("tr", true));

        Assert.Equal("tr", await ResolveAsync(null, null, null));
    }
}
