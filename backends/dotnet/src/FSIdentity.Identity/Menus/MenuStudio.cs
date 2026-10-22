using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Common;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Menus;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Menus;

public class MenuStudioChange
{
    [Required, StringLength(16)]
    public string Kind { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long MenuId { get; init; }

    public int? Order { get; init; }

    [Range(1, long.MaxValue)]
    public long? CompanyTypeId { get; init; }

    public bool? On { get; init; }

    [StringLength(8)]
    public string? LanguageCode { get; init; }

    [StringLength(256)]
    public string? Title { get; init; }
}

public class MenuStudioDraft
{
    [Range(1, long.MaxValue)]
    public long ApplicationTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? CompanyTypeId { get; init; }

    [StringLength(512)]
    public string? Note { get; init; }

    [Required]
    public List<MenuStudioChange> Changes { get; init; } = [];
}

public class MenuStudioImpact
{
    public int Companies { get; init; }
    public int Users { get; init; }
    public int Changes { get; init; }
    public int Hidden { get; init; }
    public int Shown { get; init; }
    public int Reordered { get; init; }
    public int Limited { get; init; }
    public int Titles { get; init; }
}

public class MenuStudioVerdict
{
    public long MenuId { get; init; }
    public bool Visible { get; init; }

    public string? Reason { get; init; }
}

public class MenuStudioNewItem
{
    [Required, StringLength(256)]
    public string TitleEn { get; init; } = string.Empty;

    [StringLength(256)]
    public string? TitleTr { get; init; }

    [Required, StringLength(256)]
    public string Route { get; init; } = string.Empty;

    [StringLength(64)]
    public string? Icon { get; init; }

    [Range(1, long.MaxValue)]
    public long? ParentId { get; init; }

    [Range(1, long.MaxValue)]
    public long? ApplicationTypeId { get; init; }

    [StringLength(64)]
    public string? PermissionCode { get; init; }

    public List<long> GrantToRoleIds { get; init; } = [];

    public List<long> CompanyTypeIds { get; init; } = [];
}

public class MenuStudioCreatedItem
{
    public long MenuId { get; init; }
    public string Code { get; init; } = string.Empty;
    public bool PermissionCreated { get; init; }
}

public interface IMenuStudioService
{
    Task<MenuStudioCreatedItem> CreateItemAsync(MenuStudioNewItem item, CancellationToken cancellationToken = default);

    Task<List<MenuStudioVerdict>> PreviewAsync(long applicationTypeId, long? companyTypeId, long? roleId, UserType? userType, CancellationToken cancellationToken = default);

    Task<MenuStudioImpact> ImpactAsync(MenuStudioDraft draft, CancellationToken cancellationToken = default);

    Task<MenuStudioImpact> PublishAsync(MenuStudioDraft draft, CancellationToken cancellationToken = default);
}

public class MenuStudioService(AppDbContext db, IHttpContextAccessor http, IApplicationScopeService applications, IPermissionEvaluator permissions) : IMenuStudioService
{
    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];
    private static readonly string[] Kinds = ["hide", "show", "order", "reset", "limit", "title"];

    public async Task<List<MenuStudioVerdict>> PreviewAsync(
        long applicationTypeId, long? companyTypeId, long? roleId, UserType? userType, CancellationToken cancellationToken = default)
    {
        var tenantId = Caller().TenantId();

        var menus = await db.Menus.AsNoTracking().Where(m => m.IsActive && (m.ApplicationTypeId == null || m.ApplicationTypeId == applicationTypeId)).ToListAsync(cancellationToken);
        var limited = await db.MenuCompanyTypes.AsNoTracking().Where(l => l.IsActive).ToListAsync(cancellationToken);
        var limitedMenus = limited.Select(l => l.MenuId).ToHashSet();
        var allowedFor = limited.Where(l => l.CompanyTypeId == companyTypeId).Select(l => l.MenuId).ToHashSet();

        var applicationLayouts = await db.MenuApplicationTypeLayouts.AsNoTracking().Where(l => l.IsActive && l.ApplicationTypeId == applicationTypeId).ToDictionaryAsync(l => l.MenuId, cancellationToken);
        var typeLayouts = companyTypeId is null
            ? []
            : await db.MenuCompanyTypeLayouts.AsNoTracking().Where(l => l.IsActive && l.CompanyTypeId == companyTypeId).ToDictionaryAsync(l => l.MenuId, cancellationToken);

        var toggles = await db.MenuFeatureToggles.IgnoreQueryFilters(AcrossTenants)
            .Where(t => t.IsActive && (t.IdentityTenantId == null || t.IdentityTenantId == tenantId)).ToListAsync(cancellationToken);
        bool FeatureOn(string code)
            => (toggles.FirstOrDefault(t => t.FeatureCode == code && t.IdentityTenantId == tenantId && tenantId != null)
                ?? toggles.FirstOrDefault(t => t.FeatureCode == code && t.IdentityTenantId == null))?.IsEnabled ?? true;

        var overrides = tenantId is null
            ? new Dictionary<long, long?>()
            : await db.MenuPermissionOverrides.IgnoreQueryFilters(AcrossTenants).Where(o => o.IsActive && o.IdentityTenantId == tenantId).ToDictionaryAsync(o => o.MenuId, o => o.PermissionId, cancellationToken);
        var codes = await db.Permissions.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);

        var imagined = roleId is not null || userType is not null;
        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleId is { } role)
        {
            held.UnionWith(await (from g in db.RolePermissions.IgnoreQueryFilters(AcrossTenants)
                                  join r in db.Roles.IgnoreQueryFilters(AcrossTenants) on g.RoleId equals r.Id
                                  join p in db.Permissions on g.PermissionId equals p.Id
                                  where g.RoleId == role && g.IsActive && r.IsActive && p.IsActive && !p.IsManagement
                                        && (r.ApplicationTypeId == null || r.ApplicationTypeId == applicationTypeId)
                                  select p.Code).ToListAsync(cancellationToken));
        }

        if (userType is { } rank)
            held.UnionWith(await db.Permissions.Where(p => p.IsActive && p.IsManagement && p.ManagementRank != null && p.ManagementRank <= rank).Select(p => p.Code).ToListAsync(cancellationToken));

        var granted = await applications.GetModuleCodesForApplicationAsync(applicationTypeId, cancellationToken);

        return menus.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(menu =>
        {
            string? reason = null;
            var hidden = typeLayouts.TryGetValue(menu.Id, out var byType) ? byType.IsHidden
                : applicationLayouts.TryGetValue(menu.Id, out var byApplication) && byApplication.IsHidden;
            if (hidden) reason = "hidden";
            else if (companyTypeId is not null && limitedMenus.Contains(menu.Id) && !allowedFor.Contains(menu.Id)) reason = "companyType";
            else if (menu.FeatureCode is { } feature && !FeatureOn(feature)) reason = "feature";
            else if (menu.ModuleCode is { } module && !granted.Contains(module)) reason = "module";
            else if (imagined)
            {
                var permissionId = overrides.TryGetValue(menu.Id, out var replaced) ? replaced : menu.PermissionId;
                if (permissionId is not null && !(codes.TryGetValue(permissionId.Value, out var code) && held.Contains(code))) reason = "permission";
            }

            return new MenuStudioVerdict { MenuId = menu.Id, Visible = reason is null, Reason = reason };
        }).ToList();
    }

    public async Task<MenuStudioImpact> ImpactAsync(MenuStudioDraft draft, CancellationToken cancellationToken = default)
    {
        await CheckAsync(draft, cancellationToken);

        var allowedTypes = db.CompanyTypeApplicationTypes.Where(m => m.IsActive && m.ApplicationTypeId == draft.ApplicationTypeId).Select(m => m.CompanyTypeId);
        var companyIds = await db.Companies.Where(c => c.IsActive && c.CompanyTypeId != null && allowedTypes.Contains(c.CompanyTypeId.Value)
                                                    && (draft.CompanyTypeId == null || c.CompanyTypeId == draft.CompanyTypeId))
            .Select(c => c.Id).ToListAsync(cancellationToken);
        var users = await db.UserCompanies.IgnoreQueryFilters(AcrossTenants).Where(m => m.IsActive && companyIds.Contains(m.CompanyId)).Select(m => m.UserId).Distinct().CountAsync(cancellationToken);

        return new MenuStudioImpact
        {
            Companies = companyIds.Count,
            Users = users,
            Changes = draft.Changes.Count,
            Hidden = draft.Changes.Count(c => c.Kind == "hide"),
            Shown = draft.Changes.Count(c => c.Kind is "show" or "reset"),
            Reordered = draft.Changes.Count(c => c.Kind == "order"),
            Limited = draft.Changes.Count(c => c.Kind == "limit"),
            Titles = draft.Changes.Count(c => c.Kind == "title"),
        };
    }

    public async Task<MenuStudioImpact> PublishAsync(MenuStudioDraft draft, CancellationToken cancellationToken = default)
    {
        var impact = await ImpactAsync(draft, cancellationToken);

        foreach (var change in draft.Changes)
        {
            switch (change.Kind)
            {
                case "hide": await UpsertLayoutAsync(draft, change.MenuId, hidden: true, order: null, keepOrder: true, cancellationToken); break;
                case "show": await UpsertLayoutAsync(draft, change.MenuId, hidden: false, order: null, keepOrder: true, cancellationToken); break;
                case "order": await UpsertLayoutAsync(draft, change.MenuId, hidden: null, order: change.Order ?? throw new BadRequestException("An order change needs an order"), keepOrder: false, cancellationToken); break;
                case "reset": await ResetLayoutAsync(draft, change.MenuId, cancellationToken); break;
                case "limit": await LimitAsync(change, cancellationToken); break;
                case "title": await TitleAsync(change, cancellationToken); break;
            }
        }

        var scope = draft.CompanyTypeId is { } type ? $"application {draft.ApplicationTypeId}, company type {type}" : $"application {draft.ApplicationTypeId}";
        db.RecordAudit("MenuStudio", draft.ApplicationTypeId.ToString(), "Publish", System.Text.Json.JsonSerializer.Serialize(new { scope, changes = draft.Changes.Count, note = draft.Note }));
        await db.SaveChangesAsync(cancellationToken);
        return impact;
    }

    public async Task<MenuStudioCreatedItem> CreateItemAsync(MenuStudioNewItem item, CancellationToken cancellationToken = default)
    {
        var route = item.Route.Trim();
        if (!route.StartsWith('/'))
            throw new BadRequestException("A route starts with /");

        Menu? parent = null;
        if (item.ParentId is { } parentId)
            parent = await db.Menus.AsNoTracking().FirstOrDefaultAsync(m => m.Id == parentId, cancellationToken) ?? throw new BadRequestException($"Menu entry {parentId} does not exist");
        var applicationId = item.ApplicationTypeId ?? parent?.ApplicationTypeId;
        if (parent?.ApplicationTypeId is { } parentApplication && applicationId != parentApplication)
            throw new BadRequestException($"The entry's application ({applicationId}) contradicts its parent's ({parentApplication})");

        var typeIds = item.CompanyTypeIds.Distinct().ToList();
        if (typeIds.Count > 0 && await db.CompanyTypes.CountAsync(t => typeIds.Contains(t.Id), cancellationToken) != typeIds.Count)
            throw new BadRequestException("A company type does not exist");

        var permissionCode = string.IsNullOrWhiteSpace(item.PermissionCode) ? null : item.PermissionCode.Trim().ToUpperInvariant();
        Permission? permission = null;
        var created = false;
        if (permissionCode is not null)
        {
            permission = await db.Permissions.FirstOrDefaultAsync(p => p.Code == permissionCode, cancellationToken);
            if (permission is null)
            {
                if (!await permissions.HasAsync(Permissions.PermissionManage, cancellationToken))
                    throw new ForbiddenException("Only someone who manages permissions can add a new one");
                permission = new Permission { Code = permissionCode, Name = permissionCode };
                db.Permissions.Add(permission);
                created = true;
            }
        }

        var roleIds = item.GrantToRoleIds.Distinct().ToList();
        if (roleIds.Count > 0)
        {
            if (permission is null)
                throw new BadRequestException("Roles can only be given a permission the entry asks for");
            if (!await permissions.HasAsync(Permissions.RoleManage, cancellationToken))
                throw new ForbiddenException("Only someone who manages roles can give a permission to roles");
            if (await db.Roles.CountAsync(r => roleIds.Contains(r.Id), cancellationToken) != roleIds.Count)
                throw new BadRequestException("A role does not exist");
        }

        var code = await UniqueCodeAsync(DeriveCode(route), cancellationToken);
        var typeId = parent?.MenuTypeId ?? await db.MenuTypes.OrderBy(t => t.Id).Select(t => (long?)t.Id).FirstOrDefaultAsync(cancellationToken);
        var next = (await db.Menus.Where(m => m.ParentId == item.ParentId).Select(m => (int?)m.DisplayOrder).MaxAsync(cancellationToken) ?? 0) + 10;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (permission is not null && created)
            await db.SaveChangesAsync(cancellationToken);

        var menu = new Menu
        {
            Code = code, Title = item.TitleEn.Trim(), Path = route, Icon = string.IsNullOrWhiteSpace(item.Icon) ? null : item.Icon.Trim(), DisplayOrder = next,
            ParentId = item.ParentId, MenuTypeId = typeId, ApplicationTypeId = applicationId, PermissionId = permission?.Id, ModuleCode = parent?.ModuleCode,
        };
        db.Menus.Add(menu);
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(item.TitleTr))
            db.MenuLocalizations.Add(new MenuLocalization { MenuId = menu.Id, LanguageCode = "tr", Title = item.TitleTr.Trim() });
        foreach (var type in typeIds)
            db.MenuCompanyTypes.Add(new MenuCompanyType { MenuId = menu.Id, CompanyTypeId = type });
        if (permission is not null)
        {
            var held = await db.RolePermissions.Where(g => g.PermissionId == permission.Id && roleIds.Contains(g.RoleId)).Select(g => g.RoleId).ToListAsync(cancellationToken);
            foreach (var role in roleIds.Except(held))
                db.RolePermissions.Add(new RolePermission { RoleId = role, PermissionId = permission.Id });
        }

        db.RecordAudit("MenuStudio", menu.Id.ToString(), "CreateItem", System.Text.Json.JsonSerializer.Serialize(new { code, route, permission = permissionCode, roles = roleIds.Count, companyTypes = typeIds.Count }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MenuStudioCreatedItem { MenuId = menu.Id, Code = code, PermissionCreated = created };
    }

    public static string DeriveCode(string route)
    {
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.StartsWith(':'))
            .Select(p => new string(p.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_').ToArray()));
        var code = string.Join('_', parts);
        return code.Length == 0 ? "HOME" : code.Length > 56 ? code[..56] : code;
    }

    private async Task<string> UniqueCodeAsync(string wanted, CancellationToken cancellationToken)
    {
        var code = wanted;
        for (var n = 2; await db.Menus.IgnoreQueryFilters().AnyAsync(m => m.Code == code, cancellationToken); n++)
            code = $"{wanted}_{n}";
        return code;
    }

    private async Task CheckAsync(MenuStudioDraft draft, CancellationToken cancellationToken)
    {
        if (!await db.ApplicationTypes.AnyAsync(a => a.Id == draft.ApplicationTypeId, cancellationToken))
            throw new BadRequestException($"ApplicationType {draft.ApplicationTypeId} does not exist");
        if (draft.CompanyTypeId is { } type && !await db.CompanyTypes.AnyAsync(t => t.Id == type, cancellationToken))
            throw new BadRequestException($"CompanyType {type} does not exist");

        var bad = draft.Changes.Select(c => c.Kind).Where(k => !Kinds.Contains(k)).Distinct().ToList();
        if (bad.Count > 0)
            throw new BadRequestException($"Unknown change: {string.Join(", ", bad)}");

        var ids = draft.Changes.Select(c => c.MenuId).Distinct().ToList();
        var known = await db.Menus.Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync(cancellationToken);
        var missing = ids.Except(known).ToList();
        if (missing.Count > 0)
            throw new BadRequestException($"Menu entry {string.Join(", ", missing)} does not exist");

        foreach (var change in draft.Changes)
        {
            if (change.Kind == "limit" && (change.CompanyTypeId is null || change.On is null))
                throw new BadRequestException("A limit change needs a company type and on or off");
            if (change.Kind == "title" && string.IsNullOrWhiteSpace(change.LanguageCode))
                throw new BadRequestException("A title change needs a language");
        }
    }

    private async Task UpsertLayoutAsync(MenuStudioDraft draft, long menuId, bool? hidden, int? order, bool keepOrder, CancellationToken cancellationToken)
    {
        if (draft.CompanyTypeId is { } type)
        {
            var row = await db.MenuCompanyTypeLayouts.FirstOrDefaultAsync(l => l.CompanyTypeId == type && l.MenuId == menuId, cancellationToken);
            if (row is null) db.MenuCompanyTypeLayouts.Add(row = new MenuCompanyTypeLayout { CompanyTypeId = type, MenuId = menuId });
            if (hidden is { } h) row.IsHidden = h;
            if (!keepOrder) row.DisplayOrder = order;
        }
        else
        {
            var row = await db.MenuApplicationTypeLayouts.FirstOrDefaultAsync(l => l.ApplicationTypeId == draft.ApplicationTypeId && l.MenuId == menuId, cancellationToken);
            if (row is null) db.MenuApplicationTypeLayouts.Add(row = new MenuApplicationTypeLayout { ApplicationTypeId = draft.ApplicationTypeId, MenuId = menuId });
            if (hidden is { } h) row.IsHidden = h;
            if (!keepOrder) row.DisplayOrder = order;
        }
    }

    private async Task ResetLayoutAsync(MenuStudioDraft draft, long menuId, CancellationToken cancellationToken)
    {
        if (draft.CompanyTypeId is { } type)
        {
            var row = await db.MenuCompanyTypeLayouts.FirstOrDefaultAsync(l => l.CompanyTypeId == type && l.MenuId == menuId, cancellationToken);
            if (row is not null) db.MenuCompanyTypeLayouts.Remove(row);
        }
        else
        {
            var row = await db.MenuApplicationTypeLayouts.FirstOrDefaultAsync(l => l.ApplicationTypeId == draft.ApplicationTypeId && l.MenuId == menuId, cancellationToken);
            if (row is not null) db.MenuApplicationTypeLayouts.Remove(row);
        }
    }

    private async Task LimitAsync(MenuStudioChange change, CancellationToken cancellationToken)
    {
        var type = change.CompanyTypeId!.Value;
        var row = await db.MenuCompanyTypes.FirstOrDefaultAsync(l => l.MenuId == change.MenuId && l.CompanyTypeId == type, cancellationToken);
        if (change.On == true && row is null) db.MenuCompanyTypes.Add(new MenuCompanyType { MenuId = change.MenuId, CompanyTypeId = type });
        else if (change.On == false && row is not null) db.MenuCompanyTypes.Remove(row);
    }

    private async Task TitleAsync(MenuStudioChange change, CancellationToken cancellationToken)
    {
        var language = change.LanguageCode!.Trim().ToLowerInvariant();
        var row = await db.MenuLocalizations.FirstOrDefaultAsync(l => l.MenuId == change.MenuId && l.LanguageCode == language, cancellationToken);
        if (string.IsNullOrWhiteSpace(change.Title)) { if (row is not null) db.MenuLocalizations.Remove(row); return; }
        if (row is null) db.MenuLocalizations.Add(new MenuLocalization { MenuId = change.MenuId, LanguageCode = language, Title = change.Title.Trim() });
        else row.Title = change.Title.Trim();
    }

    private System.Security.Claims.ClaimsPrincipal Caller() => http.HttpContext?.User ?? throw new UnauthorizedException();
}

[ApiController]
[Route("api/[controller]")]
[RequirePermission(Permissions.MenuDesign)]
public class MenuStudioController(IMenuStudioService studio) : ControllerBase
{
    [HttpGet("preview")]
    public async Task<ActionResult<ApiResponse<List<MenuStudioVerdict>>>> Preview(
        [FromQuery] long applicationTypeId, [FromQuery] long? companyTypeId, [FromQuery] long? roleId, [FromQuery] UserType? userType, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<MenuStudioVerdict>>.Success(await studio.PreviewAsync(applicationTypeId, companyTypeId, roleId, userType, cancellationToken), "Menu preview retrieved"));

    [HttpPost("items")]
    public async Task<ActionResult<ApiResponse<MenuStudioCreatedItem>>> CreateItem([FromBody] MenuStudioNewItem item, CancellationToken cancellationToken)
    {
        var created = await studio.CreateItemAsync(item, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<MenuStudioCreatedItem>.Success(created, "Menu entry created"));
    }

    [HttpPost("impact")]
    public async Task<ActionResult<ApiResponse<MenuStudioImpact>>> Impact([FromBody] MenuStudioDraft draft, CancellationToken cancellationToken)
        => Ok(ApiResponse<MenuStudioImpact>.Success(await studio.ImpactAsync(draft, cancellationToken), "Impact worked out"));

    [HttpPost("publish")]
    public async Task<ActionResult<ApiResponse<MenuStudioImpact>>> Publish([FromBody] MenuStudioDraft draft, CancellationToken cancellationToken)
        => Ok(ApiResponse<MenuStudioImpact>.Success(await studio.PublishAsync(draft, cancellationToken), "Menu published"));
}
