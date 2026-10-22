using FSIdentity.Domain.Data;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Authorization;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Security;

public interface ISoDGuard
{
    Task EnsureRoleMayGainAsync(long roleId, long addedPermissionId, CancellationToken cancellationToken = default);

    Task EnsureUserMayGainAsync(long userId, long companyId, IReadOnlyCollection<long> addedPermissionIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<long>> PermissionsOfRoleAsync(long roleId, CancellationToken cancellationToken = default);

    Task EnsureNoRoleBreaksAsync(long permissionAId, long permissionBId, CancellationToken cancellationToken = default);
}

public class SoDGuard(AppDbContext db, PermissionResolver resolver) : ISoDGuard
{
    private sealed record Rule(string Name, long A, long B);

    public async Task EnsureRoleMayGainAsync(long roleId, long addedPermissionId, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(r => r.Id == roleId).Select(r => new { r.IdentityTenantId, r.Name }).FirstOrDefaultAsync(cancellationToken);
        if (role is null)
            return;

        var rules = await RulesAsync(role.IdentityTenantId, cancellationToken);
        if (rules.Count == 0)
            return;

        var held = (await PermissionsOfRoleAsync(roleId, cancellationToken)).Append(addedPermissionId).ToHashSet();
        await ThrowIfBrokenAsync(rules, held, $"The role '{role.Name}'", cancellationToken);

        var holders = await db.UserCompanyRoles.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(h => h.RoleId == roleId && h.IsActive)
            .Select(h => new { h.UserId, h.CompanyId }).Distinct().Take(1000)
            .ToListAsync(cancellationToken);
        foreach (var holder in holders)
            await EnsureUserMayGainAsync(holder.UserId, holder.CompanyId, [addedPermissionId], cancellationToken);
    }

    public async Task EnsureUserMayGainAsync(
        long userId, long companyId, IReadOnlyCollection<long> addedPermissionIds, CancellationToken cancellationToken = default)
    {
        if (addedPermissionIds.Count == 0)
            return;

        var tenantId = await db.Users.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(u => u.Id == userId).Select(u => u.IdentityTenantId).FirstOrDefaultAsync(cancellationToken);
        var rules = await RulesAsync(tenantId, cancellationToken);
        if (rules.Count == 0)
            return;

        var current = await resolver.ResolveAsync(userId, companyId, applicationTypeId: null, cancellationToken, allApplications: true);
        var ids = await db.Permissions.Where(p => current.Codes.Contains(p.Code)).Select(p => p.Id).ToListAsync(cancellationToken);

        await ThrowIfBrokenAsync(rules, ids.Concat(addedPermissionIds).ToHashSet(), "This person", cancellationToken);
    }

    public async Task<IReadOnlyList<long>> PermissionsOfRoleAsync(long roleId, CancellationToken cancellationToken = default)
        => await db.RolePermissions.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(g => g.RoleId == roleId && g.IsActive).Select(g => g.PermissionId).ToListAsync(cancellationToken);

    public async Task EnsureNoRoleBreaksAsync(long permissionAId, long permissionBId, CancellationToken cancellationToken = default)
    {
        var broken = await db.RolePermissions.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(g => g.IsActive && (g.PermissionId == permissionAId || g.PermissionId == permissionBId))
            .GroupBy(g => g.RoleId)
            .Where(group => group.Select(g => g.PermissionId).Distinct().Count() == 2)
            .Select(group => group.Key)
            .FirstOrDefaultAsync(cancellationToken);

        if (broken != 0)
        {
            var name = await db.Roles.IgnoreQueryFilters([FSDbContext.TenantFilter])
                .Where(r => r.Id == broken).Select(r => r.Name).FirstAsync(cancellationToken);
            throw new SodConflictException($"The role '{name}' already holds both permissions");
        }
    }

    private async Task<List<Rule>> RulesAsync(long? tenantId, CancellationToken cancellationToken)
        => await db.SoDRules.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(r => r.IsActive && (r.IdentityTenantId == null || r.IdentityTenantId == tenantId))
            .Select(r => new Rule(r.Name, r.PermissionAId, r.PermissionBId))
            .ToListAsync(cancellationToken);

    private async Task ThrowIfBrokenAsync(List<Rule> rules, HashSet<long> held, string who, CancellationToken cancellationToken)
    {
        var broken = rules.FirstOrDefault(r => held.Contains(r.A) && held.Contains(r.B));
        if (broken is null)
            return;

        var codes = await db.Permissions.Where(p => p.Id == broken.A || p.Id == broken.B).Select(p => p.Code).ToListAsync(cancellationToken);
        throw new SodConflictException(
            $"{who} would hold both {string.Join(" and ", codes.Select(c => $"'{c}'"))}, which the rule '{broken.Name}' keeps apart");
    }
}
