using System.Text.Json;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Workspace;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Modules;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Workspace;

public static class DashboardLayoutCodec
{
    public const int Columns = 24;

    public const int MaxItems = 50;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static List<DashboardPlacement> Read(string json)
    {
        List<DashboardPlacement>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<DashboardPlacement>>(json, Json);
        }
        catch (JsonException)
        {
            throw new BadRequestException("layoutJson is not a list of {widget, x, y, w, h}");
        }

        return items ?? throw new BadRequestException("layoutJson must be a list");
    }

    public static string Write(IEnumerable<DashboardPlacement> items) => JsonSerializer.Serialize(
        items.Select(i => new { widget = i.Widget.Trim().ToUpperInvariant(), x = i.X, y = i.Y, w = i.W, h = i.H }), Json);

    public static void Validate(IReadOnlyList<DashboardPlacement> items)
    {
        if (items.Count > MaxItems)
            throw new BadRequestException($"A dashboard holds at most {MaxItems} widgets");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Widget))
                throw new BadRequestException("Every placement needs a widget");
            if (!seen.Add(item.Widget.Trim()))
                throw new BadRequestException($"Widget '{item.Widget}' is placed twice");
            if (item.X < 0 || item.Y < 0 || item.W < 1 || item.H < 1 || item.W > Columns || item.X + item.W > Columns)
                throw new BadRequestException($"Widget '{item.Widget}' does not fit the {Columns}-column grid");
        }
    }
}

public interface IDashboardService
{
    Task<DashboardView> MineAsync(CancellationToken cancellationToken = default);

    Task<DashboardView> PreviewAsync(long applicationTypeId, long? companyId, long? roleId, UserType? userType, CancellationToken cancellationToken = default);

    Task<DashboardView> SaveMineAsync(DashboardLayoutWrite layout, CancellationToken cancellationToken = default);

    Task ResetMineAsync(CancellationToken cancellationToken = default);

    Task EnsureWidgetsExistAsync(IEnumerable<DashboardPlacement> items, CancellationToken cancellationToken = default);
}

public class DashboardService(
    AppDbContext db,
    IHttpContextAccessor http,
    IPermissionEvaluator permissions,
    IModuleScopeAccessor modules,
    IApplicationScopeService applications) : IDashboardService
{
    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    public async Task<DashboardView> MineAsync(CancellationToken cancellationToken = default)
    {
        var user = Caller();
        var companyId = user.ActiveCompanyId();
        var applicationId = user.ActiveApplicationTypeId();
        var tenantId = user.TenantId();

        var companyTypeId = companyId is null
            ? null
            : await db.Companies.IgnoreQueryFilters(AcrossTenants)
                .Where(c => c.Id == companyId).Select(c => c.CompanyTypeId).FirstOrDefaultAsync(cancellationToken);

        var roleIds = new List<long>();
        UserType? rank = null;
        if (user.UserId() is { } userId && companyId is { } company)
        {
            roleIds = await db.UserCompanyRoles.IgnoreQueryFilters(AcrossTenants)
                .Where(r => r.UserId == userId && r.CompanyId == company && r.IsActive).Select(r => r.RoleId).Distinct().ToListAsync(cancellationToken);
            rank = await db.UserCompanies.IgnoreQueryFilters(AcrossTenants)
                .Where(m => m.UserId == userId && m.CompanyId == company && m.IsActive).Select(m => (UserType?)m.MembershipType).FirstOrDefaultAsync(cancellationToken);
        }

        var held = await permissions.GetAllAsync(cancellationToken);
        var context = new Resolution(user.UserId(), tenantId, companyId, companyTypeId, applicationId, roleIds, rank, held, user.IsSuperAdmin(),
            code => modules.IsGrantedAsync(code, cancellationToken));
        return await ResolveAsync(context, cancellationToken);
    }

    public async Task<DashboardView> PreviewAsync(long applicationTypeId, long? companyId, long? roleId, UserType? userType, CancellationToken cancellationToken = default)
    {
        var user = Caller();
        var tenantId = user.TenantId();

        long? companyTypeId = null;
        if (companyId is { } company)
        {
            var row = await db.Companies.IgnoreQueryFilters(AcrossTenants).Where(c => c.Id == company && (tenantId == null || c.IdentityTenantId == tenantId))
                .Select(c => new { c.CompanyTypeId, c.IdentityTenantId }).FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException($"Company {company} not found");
            companyTypeId = row.CompanyTypeId;
            tenantId ??= row.IdentityTenantId;
        }

        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleId is { } role)
        {
            var grants = await (from g in db.RolePermissions.IgnoreQueryFilters(AcrossTenants)
                                join r in db.Roles.IgnoreQueryFilters(AcrossTenants) on g.RoleId equals r.Id
                                join p in db.Permissions on g.PermissionId equals p.Id
                                where g.RoleId == role && g.IsActive && r.IsActive && p.IsActive && !p.IsManagement
                                      && (r.ApplicationTypeId == null || r.ApplicationTypeId == applicationTypeId)
                                select p.Code).ToListAsync(cancellationToken);
            held.UnionWith(grants);
        }

        if (userType is { } rank)
        {
            var byRank = await db.Permissions.Where(p => p.IsActive && p.IsManagement && p.ManagementRank != null && p.ManagementRank <= rank).Select(p => p.Code).ToListAsync(cancellationToken);
            held.UnionWith(byRank);
        }

        var granted = await applications.GetModuleCodesForApplicationAsync(applicationTypeId, cancellationToken);
        var context = new Resolution(null, tenantId, companyId, companyTypeId, applicationTypeId, roleId is { } r2 ? [r2] : [], userType, held, false,
            code => Task.FromResult(granted.Contains(code)));
        return await ResolveAsync(context, cancellationToken);
    }

    private sealed record Resolution(
        long? UserId, long? TenantId, long? CompanyId, long? CompanyTypeId, long? ApplicationId, IReadOnlyList<long> RoleIds, UserType? Rank,
        IReadOnlySet<string> Held, bool IsPlatformAdmin, Func<string, Task<bool>> ModuleGranted);

    private async Task<DashboardView> ResolveAsync(Resolution who, CancellationToken cancellationToken)
    {
        var wanted = new List<(string Name, DashboardScope Scope, long Id, long? Company)>();
        if (who.UserId is { } userId) wanted.Add(("user", DashboardScope.User, userId, null));
        if (who.CompanyId is { } inCompany)
            foreach (var role in who.RoleIds) wanted.Add(("roleInCompany", DashboardScope.Role, role, inCompany));
        foreach (var role in who.RoleIds) wanted.Add(("role", DashboardScope.Role, role, null));
        if (who.Rank is { } rank)
        {
            if (who.CompanyId is { } c) wanted.Add(("userTypeInCompany", DashboardScope.UserType, (long)rank, c));
            wanted.Add(("userType", DashboardScope.UserType, (long)rank, null));
        }

        if (who.CompanyId is { } company) wanted.Add(("company", DashboardScope.Company, company, null));
        if (who.TenantId is { } tenant) wanted.Add(("tenant", DashboardScope.Tenant, tenant, null));
        if (who.CompanyTypeId is { } type) wanted.Add(("companyType", DashboardScope.CompanyType, type, null));
        if (who.ApplicationId is { } application) wanted.Add(("application", DashboardScope.Application, application, null));

        var candidates = await db.DashboardLayouts.IgnoreQueryFilters(AcrossTenants)
            .Where(l => l.IsActive && (l.IdentityTenantId == null || l.IdentityTenantId == who.TenantId)
                        && (l.ApplicationTypeId == null || l.ApplicationTypeId == who.ApplicationId))
            .ToListAsync(cancellationToken);

        var widgets = await db.DashboardWidgets.AsNoTracking().Where(w => w.IsActive).ToListAsync(cancellationToken);
        var componentTypes = await db.ComponentTypes.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Code, cancellationToken);
        var codes = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);

        var allowed = new Dictionary<string, DashboardWidget>(StringComparer.OrdinalIgnoreCase);
        foreach (var widget in widgets.OrderBy(w => w.Code, StringComparer.Ordinal))
        {
            var permitted = widget.PermissionId is null || who.IsPlatformAdmin
                            || (codes.TryGetValue(widget.PermissionId.Value, out var code) && who.Held.Contains(code));
            var granted = widget.ModuleCode is null || await who.ModuleGranted(widget.ModuleCode);
            if (permitted && granted)
                allowed[widget.Code] = widget;
        }

        foreach (var (name, scope, id, forCompany) in wanted)
        {
            var layout = candidates.Where(l => l.Scope == scope && l.ScopeId == id && l.CompanyId == forCompany)
                .OrderByDescending(l => l.IdentityTenantId != null).ThenByDescending(l => l.ApplicationTypeId != null).FirstOrDefault();
            if (layout is null)
                continue;

            var placed = DashboardLayoutCodec.Read(layout.LayoutJson);
            var visible = placed.Where(p => allowed.ContainsKey(p.Widget.Trim())).ToList();
            return new DashboardView
            {
                Source = name,
                SourceScope = scope,
                SourceScopeId = id,
                SourceCompanyId = forCompany,
                LayoutId = layout.Id,
                HiddenCount = placed.Count - visible.Count,
                Items = visible.Select(p => Item(allowed[p.Widget.Trim()], p, componentTypes)).ToList(),
            };
        }

        var items = new List<DashboardItem>();
        int x = 0, y = 0, rowHeight = 0;
        foreach (var widget in allowed.Values)
        {
            if (x + widget.DefaultWidth > DashboardLayoutCodec.Columns)
            {
                x = 0;
                y += rowHeight;
                rowHeight = 0;
            }

            items.Add(Item(widget, new DashboardPlacement { X = x, Y = y, W = widget.DefaultWidth, H = widget.DefaultHeight }, componentTypes));
            x += widget.DefaultWidth;
            rowHeight = Math.Max(rowHeight, widget.DefaultHeight);
        }

        return new DashboardView { Source = "default", Items = items };
    }

    public async Task<DashboardView> SaveMineAsync(DashboardLayoutWrite layout, CancellationToken cancellationToken = default)
    {
        var user = Caller();
        var userId = user.UserId() ?? throw new UnauthorizedException();
        DashboardLayoutCodec.Validate(layout.Items);
        await EnsureWidgetsExistAsync(layout.Items, cancellationToken);

        var tenantId = user.TenantId();
        var mine = await db.DashboardLayouts.IgnoreQueryFilters(AcrossTenants)
            .FirstOrDefaultAsync(l => l.Scope == DashboardScope.User && l.ScopeId == userId, cancellationToken);
        if (mine is null)
        {
            mine = new DashboardLayout { Name = "My dashboard", Scope = DashboardScope.User, ScopeId = userId, IdentityTenantId = tenantId };
            db.DashboardLayouts.Add(mine);
        }

        mine.LayoutJson = DashboardLayoutCodec.Write(layout.Items);
        await db.SaveChangesAsync(cancellationToken);
        return await MineAsync(cancellationToken);
    }

    public async Task ResetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = Caller().UserId() ?? throw new UnauthorizedException();
        var mine = await db.DashboardLayouts.IgnoreQueryFilters(AcrossTenants)
            .FirstOrDefaultAsync(l => l.Scope == DashboardScope.User && l.ScopeId == userId, cancellationToken);
        if (mine is null)
            return;

        db.DashboardLayouts.Remove(mine);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureWidgetsExistAsync(IEnumerable<DashboardPlacement> items, CancellationToken cancellationToken = default)
    {
        var wanted = items.Select(i => i.Widget.Trim().ToUpperInvariant()).Distinct().ToList();
        if (wanted.Count == 0)
            return;

        var known = await db.DashboardWidgets.Where(w => w.IsActive && wanted.Contains(w.Code)).Select(w => w.Code).ToListAsync(cancellationToken);
        var missing = wanted.Except(known, StringComparer.OrdinalIgnoreCase).ToList();
        if (missing.Count > 0)
            throw new BadRequestException($"Unknown widget: {string.Join(", ", missing)}");
    }

    private static DashboardItem Item(DashboardWidget widget, DashboardPlacement place, Dictionary<long, string> componentTypes) => new()
    {
        Widget = widget.Code,
        Title = widget.Title,
        ComponentType = widget.ComponentTypeId is { } id && componentTypes.TryGetValue(id, out var type) ? type : null,
        X = place.X,
        Y = place.Y,
        W = place.W,
        H = place.H,
    };

    private System.Security.Claims.ClaimsPrincipal Caller() => http.HttpContext?.User ?? throw new UnauthorizedException();
}
