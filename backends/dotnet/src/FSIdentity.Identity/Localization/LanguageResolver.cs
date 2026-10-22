using FSIdentity.Domain.Data;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Localization;

public interface ILanguageResolver
{
    Task<string> ResolveAsync(long? userId, long? companyId, long? tenantId, CancellationToken cancellationToken = default);
}

public class LanguageResolver(AppDbContext db) : ILanguageResolver
{
    public const string LastResort = "en";

    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    public async Task<string> ResolveAsync(long? userId, long? companyId, long? tenantId, CancellationToken cancellationToken = default)
    {
        var candidates = new List<string?>();

        if (userId is { } user)
            candidates.Add(await db.Users.IgnoreQueryFilters(AcrossTenants)
                .Where(u => u.Id == user).Select(u => u.LanguageCode).FirstOrDefaultAsync(cancellationToken));
        if (companyId is { } company)
            candidates.Add(await db.Companies.IgnoreQueryFilters(AcrossTenants)
                .Where(c => c.Id == company).Select(c => c.LanguageCode).FirstOrDefaultAsync(cancellationToken));
        if (tenantId is { } tenant)
            candidates.Add(await db.Tenants.Where(t => t.Id == tenant).Select(t => t.LanguageCode).FirstOrDefaultAsync(cancellationToken));

        var live = await db.Languages.Where(l => l.IsActive).Select(l => l.Code).ToListAsync(cancellationToken);
        var known = new HashSet<string>(live, StringComparer.OrdinalIgnoreCase);

        foreach (var code in candidates)
            if (!string.IsNullOrWhiteSpace(code) && known.Contains(code.Trim()))
                return code.Trim().ToLowerInvariant();

        var fallback = await db.Languages.Where(l => l.IsActive && l.IsFallback).Select(l => l.Code).FirstOrDefaultAsync(cancellationToken);
        return (fallback ?? LastResort).ToLowerInvariant();
    }
}
