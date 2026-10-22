using FSIdentity.Domain.Modules;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Applications;

public class ModuleScopeAccessor(
    IApplicationScopeService applications,
    IHttpContextAccessor http,
    IMemoryCache cache,
    IOptions<ModuleScopeOptions> options) : IModuleScopeAccessor
{
    private HashSet<string>? _resolved;

    public async Task<HashSet<string>> GetGrantedCodesAsync(CancellationToken cancellationToken = default)
    {
        if (_resolved is not null)
            return _resolved;

        var applicationTypeId = await ResolveApplicationAsync(cancellationToken);
        if (applicationTypeId is null)
            return _resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var seconds = options.Value.CacheSeconds;
        if (seconds <= 0)
            return _resolved = await applications.GetModuleCodesForApplicationAsync(applicationTypeId, cancellationToken);

        var key = $"module-scope:app:{applicationTypeId.Value}";
        if (cache.TryGetValue(key, out HashSet<string>? cached) && cached is not null)
            return _resolved = cached;

        var codes = await applications.GetModuleCodesForApplicationAsync(applicationTypeId, cancellationToken);
        cache.Set(key, codes, TimeSpan.FromSeconds(seconds));
        return _resolved = codes;
    }

    public async Task<bool> IsGrantedAsync(string moduleCode, CancellationToken cancellationToken = default)
    {
        if (!await applications.AnyModuleGrantsConfiguredAsync(cancellationToken))
            return true;

        if (await ResolveApplicationAsync(cancellationToken) is null)
            return !options.Value.StrictMode;

        return (await GetGrantedCodesAsync(cancellationToken)).Contains(moduleCode);
    }

    private async Task<long?> ResolveApplicationAsync(CancellationToken cancellationToken)
    {
        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return null;

        if (!long.TryParse(principal.FindFirst(ClaimNames.ActiveCompanyId)?.Value, out var companyId))
            return null;

        long? onToken = long.TryParse(principal.FindFirst(ClaimNames.ActiveApplicationTypeId)?.Value, out var id) ? id : null;

        return await applications.ResolveForSessionAsync(companyId, onToken, cancellationToken: cancellationToken);
    }
}