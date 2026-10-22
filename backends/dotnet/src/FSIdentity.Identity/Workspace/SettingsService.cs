using System.Text.RegularExpressions;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Workspace;

public interface ISettingsService
{
    Task<List<SettingValue>> EffectiveAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    Task<List<SettingValue>> MineAsync(CancellationToken cancellationToken = default);

    Task<SettingValue> SetMineAsync(string key, string? value, CancellationToken cancellationToken = default);

    Task<bool> DeleteMineAsync(string key, CancellationToken cancellationToken = default);
}

public partial class SettingsService(AppDbContext db, IHttpContextAccessor http) : ISettingsService
{
    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    public const int MaxPerUser = 200;

    [GeneratedRegex(SettingKeys.Pattern)]
    private static partial Regex KeyPattern();

    public async Task<List<SettingValue>> EffectiveAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        var user = Caller();
        var wanted = keys.Select(k => k.Trim()).Where(k => k.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0)
            return [];

        var result = new Dictionary<string, SettingValue>(StringComparer.OrdinalIgnoreCase);

        if (user.TenantId() is { } tenantId)
            foreach (var s in await db.TenantSettings.IgnoreQueryFilters(AcrossTenants)
                         .Where(s => s.IsActive && s.IdentityTenantId == tenantId && wanted.Contains(s.Key)).ToListAsync(cancellationToken))
                result[s.Key] = new SettingValue { Key = s.Key, Value = s.Value, Source = "tenant" };

        if (user.ActiveCompanyId() is { } companyId)
            foreach (var s in await db.CompanySettings.IgnoreQueryFilters([FSDbContext.CompanyFilter, FSDbContext.TenantFilter])
                         .Where(s => s.IsActive && s.IdentityCompanyId == companyId && wanted.Contains(s.Key)).ToListAsync(cancellationToken))
                result[s.Key] = new SettingValue { Key = s.Key, Value = s.Value, Source = "company" };

        if (user.UserId() is { } userId)
            foreach (var s in await db.UserSettings.Where(s => s.IsActive && s.UserId == userId && wanted.Contains(s.Key)).ToListAsync(cancellationToken))
                result[s.Key] = new SettingValue { Key = s.Key, Value = s.Value, Source = "user" };

        return result.Values.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task<List<SettingValue>> MineAsync(CancellationToken cancellationToken = default)
    {
        var userId = Caller().UserId() ?? throw new UnauthorizedException();
        return await db.UserSettings.AsNoTracking().Where(s => s.UserId == userId && s.IsActive)
            .OrderBy(s => s.Key)
            .Select(s => new SettingValue { Key = s.Key, Value = s.Value, Source = "user" })
            .ToListAsync(cancellationToken);
    }

    public async Task<SettingValue> SetMineAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        var userId = Caller().UserId() ?? throw new UnauthorizedException();
        key = key.Trim();
        if (!KeyPattern().IsMatch(key))
            throw new BadRequestException("A key starts with a letter and holds letters, digits, '.', '-' and '_' (at most 64)");

        var existing = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key, cancellationToken);
        if (existing is null)
        {
            if (await db.UserSettings.CountAsync(s => s.UserId == userId, cancellationToken) >= MaxPerUser)
                throw new BadRequestException($"A person can keep at most {MaxPerUser} settings");

            existing = new Domain.Entities.Workspace.UserSetting { UserId = userId, Key = key };
            db.UserSettings.Add(existing);
        }

        existing.Value = value;
        await db.SaveChangesAsync(cancellationToken);
        return new SettingValue { Key = existing.Key, Value = existing.Value, Source = "user" };
    }

    public async Task<bool> DeleteMineAsync(string key, CancellationToken cancellationToken = default)
    {
        var userId = Caller().UserId() ?? throw new UnauthorizedException();
        var existing = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId && s.Key == key.Trim(), cancellationToken);
        if (existing is null)
            return false;

        db.UserSettings.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private System.Security.Claims.ClaimsPrincipal Caller() => http.HttpContext?.User ?? throw new UnauthorizedException();
}
