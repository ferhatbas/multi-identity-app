using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Authorization;

public record EffectivePermission(string Code, string Name, IReadOnlyList<string> Via);

public record EffectivePermissions(IReadOnlyList<EffectivePermission> Granted, IReadOnlyList<string> Denied)
{
    public IReadOnlySet<string> Codes { get; } = new HashSet<string>(Granted.Select(g => g.Code), StringComparer.OrdinalIgnoreCase);
}

public class PermissionResolver(AppDbContext db)
{
    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    public async Task<EffectivePermissions> ResolveAsync(
        long userId, long companyId, long? applicationTypeId, CancellationToken cancellationToken = default,
        bool allApplications = false)
    {
        var membership = await db.UserCompanies.IgnoreQueryFilters(AcrossTenants)
            .Where(m => m.UserId == userId && m.CompanyId == companyId && m.IsActive)
            .Select(m => (UserType?)m.MembershipType)
            .FirstOrDefaultAsync(cancellationToken);
        if (membership is null)
            return new EffectivePermissions([], []);

        var fromRoles = await (
            from held in db.UserCompanyRoles.IgnoreQueryFilters(AcrossTenants)
            join role in db.Roles.IgnoreQueryFilters(AcrossTenants) on held.RoleId equals role.Id
            join grant in db.RolePermissions.IgnoreQueryFilters(AcrossTenants) on role.Id equals grant.RoleId
            join permission in db.Permissions on grant.PermissionId equals permission.Id
            where held.UserId == userId && held.CompanyId == companyId && held.IsActive
                  && role.IsActive && grant.IsActive && permission.IsActive && !permission.IsManagement
                  && (allApplications || role.ApplicationTypeId == null || role.ApplicationTypeId == applicationTypeId)
            select new { permission.Code, permission.Name, Role = role.Name }).ToListAsync(cancellationToken);

        var overrides = await (
            from direct in db.UserCompanyPermissions.IgnoreQueryFilters(AcrossTenants)
            join permission in db.Permissions on direct.PermissionId equals permission.Id
            where direct.UserId == userId && direct.CompanyId == companyId && direct.IsActive && permission.IsActive
                  && !permission.IsManagement
            select new { permission.Code, permission.Name, direct.IsGranted }).ToListAsync(cancellationToken);

        var byRank = await db.Permissions
            .Where(p => p.IsActive && p.IsManagement && p.ManagementRank != null && p.ManagementRank <= membership)
            .Select(p => new { p.Code, p.Name })
            .ToListAsync(cancellationToken);

        var granted = fromRoles
            .GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (Name: g.First().Name, Via: g.Select(r => $"role:{r.Role}").Distinct().ToList()),
                StringComparer.OrdinalIgnoreCase);

        foreach (var permission in byRank)
            granted[permission.Code] = (permission.Name, [$"rank:{membership}"]);

        var denied = new List<string>();
        foreach (var direct in overrides)
        {
            if (direct.IsGranted)
            {
                if (granted.TryGetValue(direct.Code, out var existing))
                    existing.Via.Add("direct");
                else
                    granted[direct.Code] = (direct.Name, ["direct"]);
            }
            else
            {
                granted.Remove(direct.Code);
                denied.Add(direct.Code);
            }
        }

        return new EffectivePermissions(
            granted.OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new EffectivePermission(g.Key, g.Value.Name, g.Value.Via)).ToList(),
            denied.OrderBy(d => d, StringComparer.Ordinal).ToList());
    }
}

public class PermissionEvaluator(PermissionResolver resolver, IHttpContextAccessor http) : IPermissionEvaluator
{
    private IReadOnlySet<string>? _all;

    public async Task<bool> HasAsync(string permissionCode, CancellationToken cancellationToken = default)
    {
        if (http.HttpContext?.User.IsSuperAdmin() == true)
            return true;

        return (await GetAllAsync(cancellationToken)).Contains(permissionCode);
    }

    public async Task<IReadOnlySet<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_all is not null)
            return _all;

        var principal = http.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true)
            return _all = new HashSet<string>();

        if (principal.IsSuperAdmin())
            return _all = new HashSet<string>(Permissions.All, StringComparer.OrdinalIgnoreCase);

        if (principal.UserId() is not { } userId || principal.ActiveCompanyId() is not { } companyId)
            return _all = new HashSet<string>();

        var effective = await resolver.ResolveAsync(userId, companyId, principal.ActiveApplicationTypeId(), cancellationToken);
        return _all = effective.Codes;
    }
}
