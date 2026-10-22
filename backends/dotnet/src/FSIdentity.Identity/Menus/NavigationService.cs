using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Menus;
using FSIdentity.Domain.Modules;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Menus;

public interface INavigationService
{
    Task<List<MenuNode>> MineAsync(CancellationToken cancellationToken = default);

    Task<List<MenuNode>> FullTreeAsync(CancellationToken cancellationToken = default);
}

public class NavigationService(
    AppDbContext db,
    IHttpContextAccessor http,
    IPermissionEvaluator permissions,
    IModuleScopeAccessor modules,
    ILanguageResolver languages) : INavigationService
{
    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    public async Task<List<MenuNode>> MineAsync(CancellationToken cancellationToken = default)
    {
        var user = http.HttpContext?.User ?? throw new InvalidOperationException("No request");
        var tenantId = user.TenantId();
        var companyId = user.ActiveCompanyId();
        var applicationId = user.ActiveApplicationTypeId();

        var companyTypeId = companyId is null
            ? null
            : await db.Companies.IgnoreQueryFilters(AcrossTenants)
                .Where(c => c.Id == companyId).Select(c => c.CompanyTypeId).FirstOrDefaultAsync(cancellationToken);

        var menus = await db.Menus.AsNoTracking()
            .Where(m => m.IsActive && (m.ApplicationTypeId == null || m.ApplicationTypeId == applicationId))
            .ToListAsync(cancellationToken);

        var limited = await db.MenuCompanyTypes.AsNoTracking().Where(l => l.IsActive).ToListAsync(cancellationToken);
        var limitedMenus = limited.Select(l => l.MenuId).ToHashSet();
        var allowedFor = limited.Where(l => l.CompanyTypeId == companyTypeId).Select(l => l.MenuId).ToHashSet();

        var applicationLayouts = applicationId is null
            ? []
            : await db.MenuApplicationTypeLayouts.AsNoTracking().Where(l => l.IsActive && l.ApplicationTypeId == applicationId)
                .ToDictionaryAsync(l => l.MenuId, cancellationToken);
        var typeLayouts = companyTypeId is null
            ? []
            : await db.MenuCompanyTypeLayouts.AsNoTracking().Where(l => l.IsActive && l.CompanyTypeId == companyTypeId)
                .ToDictionaryAsync(l => l.MenuId, cancellationToken);

        var toggles = await db.MenuFeatureToggles.IgnoreQueryFilters(AcrossTenants)
            .Where(t => t.IsActive && (t.IdentityTenantId == null || t.IdentityTenantId == tenantId))
            .ToListAsync(cancellationToken);
        bool FeatureOn(string code)
            => (toggles.FirstOrDefault(t => t.FeatureCode == code && t.IdentityTenantId == tenantId && tenantId != null)
                ?? toggles.FirstOrDefault(t => t.FeatureCode == code && t.IdentityTenantId == null))?.IsEnabled ?? true;

        var overrides = tenantId is null
            ? new Dictionary<long, long?>()
            : await db.MenuPermissionOverrides.IgnoreQueryFilters(AcrossTenants)
                .Where(o => o.IsActive && o.IdentityTenantId == tenantId)
                .ToDictionaryAsync(o => o.MenuId, o => o.PermissionId, cancellationToken);
        var codes = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);
        var held = await permissions.GetAllAsync(cancellationToken);
        var isPlatformAdmin = user.IsSuperAdmin();

        var moduleOk = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in menus.Select(m => m.ModuleCode).Where(c => c is not null).Distinct())
            moduleOk[code!] = await modules.IsGrantedAsync(code!, cancellationToken);

        var language = await languages.ResolveAsync(user.UserId(), companyId, tenantId, cancellationToken);
        var titles = await db.MenuLocalizations.AsNoTracking()
            .Where(l => l.IsActive && l.LanguageCode == language)
            .ToDictionaryAsync(l => l.MenuId, l => l.Title, cancellationToken);

        bool Visible(Menu menu)
        {
            var hidden = typeLayouts.TryGetValue(menu.Id, out var byType) ? byType.IsHidden
                : applicationLayouts.TryGetValue(menu.Id, out var byApplication) && byApplication.IsHidden;
            if (hidden)
                return false;
            if (limitedMenus.Contains(menu.Id) && !allowedFor.Contains(menu.Id))
                return false;
            if (menu.FeatureCode is { } feature && !FeatureOn(feature))
                return false;
            if (menu.ModuleCode is { } module && !moduleOk[module])
                return false;

            var permissionId = overrides.TryGetValue(menu.Id, out var replaced) ? replaced : menu.PermissionId;
            return permissionId is null || isPlatformAdmin || (codes.TryGetValue(permissionId.Value, out var code) && held.Contains(code));
        }

        int Order(Menu menu) => typeLayouts.TryGetValue(menu.Id, out var byType) && byType.DisplayOrder is { } a ? a
            : applicationLayouts.TryGetValue(menu.Id, out var byApplication) && byApplication.DisplayOrder is { } b ? b
            : menu.DisplayOrder;

        return Build(menus.Where(Visible).ToList(), Order, m => titles.TryGetValue(m.Id, out var title) ? title : m.Title, pruneEmptyGroups: true);
    }

    public async Task<List<MenuNode>> FullTreeAsync(CancellationToken cancellationToken = default)
    {
        var menus = await db.Menus.AsNoTracking().ToListAsync(cancellationToken);
        return Build(menus, m => m.DisplayOrder, m => m.Title, pruneEmptyGroups: false);
    }

    private static List<MenuNode> Build(List<Menu> menus, Func<Menu, int> order, Func<Menu, string> title, bool pruneEmptyGroups)
    {
        var byParent = menus.ToLookup(m => m.ParentId);

        List<MenuNode> Children(long? parentId) => byParent[parentId]
            .OrderBy(order).ThenBy(m => m.Id)
            .Select(m =>
            {
                var children = Children(m.Id);
                return new MenuNode
                {
                    Id = m.Id,
                    Code = m.Code,
                    Title = title(m),
                    Path = m.Path,
                    Icon = m.Icon,
                    DisplayOrder = order(m),
                    Children = children,
                };
            })
            .Where(node => !pruneEmptyGroups || node.Path is not null || node.Children.Count > 0)
            .ToList();

        return Children(null);
    }
}
