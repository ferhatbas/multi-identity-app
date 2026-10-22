using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Organization;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Applications;

public interface IApplicationScopeService
{
    Task<List<ApplicationType>> GetAllowedForCompanyAsync(long companyId, CancellationToken cancellationToken = default);

    Task<long?> ResolveActiveAsync(long companyId, long? preferredApplicationTypeId, CancellationToken cancellationToken = default);

    Task<long?> ResolveForSessionAsync(
        long companyId, long? sessionApplicationTypeId, long? rememberedApplicationTypeId = null,
        CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetModuleCodesForApplicationAsync(long? applicationTypeId, CancellationToken cancellationToken = default);

    Task<bool> AnyModuleGrantsConfiguredAsync(CancellationToken cancellationToken = default);
}

public class ApplicationScopeService(IUnitOfWork uow) : IApplicationScopeService
{
    private sealed record Allowed(ApplicationType Application, bool IsDefault);

    private sealed record Scope(string? CompanyTypeCode, List<Allowed> Applications);

    public async Task<List<ApplicationType>> GetAllowedForCompanyAsync(
        long companyId, CancellationToken cancellationToken = default)
        => (await LoadScopeAsync(companyId, cancellationToken)).Applications.Select(a => a.Application).ToList();

    public async Task<long?> ResolveActiveAsync(
        long companyId, long? preferredApplicationTypeId, CancellationToken cancellationToken = default)
        => ResolveHome(await LoadScopeAsync(companyId, cancellationToken), preferredApplicationTypeId);

    public async Task<long?> ResolveForSessionAsync(
        long companyId, long? sessionApplicationTypeId, long? rememberedApplicationTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await LoadScopeAsync(companyId, cancellationToken);

        if (sessionApplicationTypeId is { } claimed && scope.Applications.Any(a => a.Application.Id == claimed))
            return claimed;

        return ResolveHome(scope, rememberedApplicationTypeId);
    }

    private static long? ResolveHome(Scope scope, long? preferredApplicationTypeId)
    {
        var allowed = scope.Applications;
        if (allowed.Count == 0)
            return null;

        var preferred = preferredApplicationTypeId is { } id ? allowed.FirstOrDefault(a => a.Application.Id == id) : null;
        if (preferred is not null && !(IsManagement(preferred.Application.Code) && !IsManagement(scope.CompanyTypeCode)))
            return preferred.Application.Id;

        if (allowed.FirstOrDefault(a => a.IsDefault) is { } flagged)
            return flagged.Application.Id;

        if (scope.CompanyTypeCode is not null
            && allowed.FirstOrDefault(a => string.Equals(a.Application.Code, scope.CompanyTypeCode,
                StringComparison.OrdinalIgnoreCase)) is { } twin)
            return twin.Application.Id;

        return (allowed.FirstOrDefault(a => !IsManagement(a.Application.Code)) ?? allowed[0]).Application.Id;
    }

    private static bool IsManagement(string? code)
        => string.Equals(code, ApplicationType.ManagementCode, StringComparison.OrdinalIgnoreCase);

    private async Task<Scope> LoadScopeAsync(long companyId, CancellationToken cancellationToken)
    {
        var companyTypeId = await uow.Repository<Company>().QueryAcrossScopes()
            .Where(c => c.Id == companyId)
            .Select(c => c.CompanyTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (companyTypeId is not { } typeId)
            return new Scope(null, []);

        var code = await uow.Repository<CompanyType>().Query()
            .Where(t => t.Id == typeId)
            .Select(t => t.Code)
            .FirstOrDefaultAsync(cancellationToken);

        var rows = await uow.Repository<CompanyTypeApplicationType>().Query()
            .Where(m => m.CompanyTypeId == typeId && m.IsActive
                        && !m.ApplicationType!.IsDeleted && m.ApplicationType.IsActive)
            .OrderBy(m => m.ApplicationType!.DisplayOrder).ThenBy(m => m.ApplicationTypeId)
            .Select(m => new { m.ApplicationType, m.IsDefault })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new Scope(code, rows.Select(r => new Allowed(r.ApplicationType!, r.IsDefault)).ToList());
    }

    public async Task<HashSet<string>> GetModuleCodesForApplicationAsync(
        long? applicationTypeId, CancellationToken cancellationToken = default)
    {
        var core = await uow.Repository<AppModule>().Query()
            .Where(m => m.IsActive && m.IsCore)
            .Select(m => m.Code)
            .ToListAsync(cancellationToken);

        var codes = new HashSet<string>(core, StringComparer.OrdinalIgnoreCase);
        if (applicationTypeId is null)
            return codes;

        var granted = await uow.Repository<ApplicationTypeModule>().Query()
            .Where(g => g.ApplicationTypeId == applicationTypeId && g.IsActive
                        && !g.AppModule!.IsDeleted && g.AppModule.IsActive)
            .Select(g => g.AppModule!.Code)
            .ToListAsync(cancellationToken);

        codes.UnionWith(granted);
        return codes;
    }

    public Task<bool> AnyModuleGrantsConfiguredAsync(CancellationToken cancellationToken = default)
        => uow.Repository<ApplicationTypeModule>().Query().AnyAsync(g => g.IsActive, cancellationToken);
}