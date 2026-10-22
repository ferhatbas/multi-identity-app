using FSIdentity.Domain.Common;
using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Entities.Workspace;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces.MultiTenant;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Workspace;

public class UnitTypeController(ICrudService<UnitType, UnitTypeRead, UnitTypeCreate, UnitTypeUpdate> service)
    : CrudController<UnitType, UnitTypeRead, UnitTypeCreate, UnitTypeUpdate>(service, "UnitType", "UnitTypes")
{
    protected override string WritePermission => Permissions.UnitManage;
}

public class UnitController(ICrudService<Unit, UnitRead, UnitCreate, UnitUpdate> service, IUnitOfWork uow)
    : CrudController<Unit, UnitRead, UnitCreate, UnitUpdate>(service, "Unit", "Units")
{
    protected override string WritePermission => Permissions.UnitManage;

    [HttpGet("organization")]
    [RequirePermission(Permissions.UnitManage)]
    public async Task<ActionResult<ApiResponse<List<OrganizationUnit>>>> Organization(CancellationToken cancellationToken)
    {
        var units = await uow.Repository<Unit>().Query().AsNoTracking()
            .IgnoreQueryFilters([FSDbContext.CompanyFilter])
            .OrderBy(u => u.Name).ThenBy(u => u.Id)
            .Select(u => new OrganizationUnit
            {
                Id = u.Id, ParentId = u.ParentId, CompanyId = u.IdentityCompanyId, Code = u.Code, Name = u.Name, UnitTypeId = u.UnitTypeId,
                ManagerUserId = u.ManagerUserId, Description = u.Description, IsActive = u.IsActive,
            }).ToListAsync(cancellationToken);
        return Ok(ApiResponse<List<OrganizationUnit>>.Success(units, "Organisation units retrieved"));
    }

    [HttpGet("tree")]
    public async Task<ActionResult<ApiResponse<List<UnitNode>>>> Tree(CancellationToken cancellationToken)
    {
        var units = await uow.Repository<Unit>().Query().AsNoTracking()
            .Where(u => u.IsActive).OrderBy(u => u.Name).ThenBy(u => u.Id).ToListAsync(cancellationToken);
        var byParent = units.ToLookup(u => u.ParentId);

        List<UnitNode> Nodes(long? parentId) => byParent[parentId].Select(u => new UnitNode
        {
            Id = u.Id, Code = u.Code, Name = u.Name, UnitTypeId = u.UnitTypeId, ManagerUserId = u.ManagerUserId, Children = Nodes(u.Id),
        }).ToList();

        return Ok(ApiResponse<List<UnitNode>>.Success(Nodes(null), "Unit tree retrieved"));
    }
}

public class UserUnitController(ICrudService<UserUnit, UserUnitRead, UserUnitCreate, UserUnitUpdate> service)
    : CrudController<UserUnit, UserUnitRead, UserUnitCreate, UserUnitUpdate>(service, "UserUnit", "UserUnits")
{
    protected override string WritePermission => Permissions.UnitManage;
}

public class TenantSettingController(ICrudService<TenantSetting, TenantSettingRead, TenantSettingCreate, TenantSettingUpdate> service)
    : CrudController<TenantSetting, TenantSettingRead, TenantSettingCreate, TenantSettingUpdate>(service, "TenantSetting", "TenantSettings")
{
    protected override string WritePermission => Permissions.SettingsManage;

    protected override string? ReadPermission => Permissions.SettingsManage;
}

public class CompanySettingController(ICrudService<CompanySetting, CompanySettingRead, CompanySettingCreate, CompanySettingUpdate> service)
    : CrudController<CompanySetting, CompanySettingRead, CompanySettingCreate, CompanySettingUpdate>(service, "CompanySetting", "CompanySettings")
{
    protected override string WritePermission => Permissions.SettingsManage;

    protected override string? ReadPermission => Permissions.SettingsManage;
}

public class ComponentTypeController(ICrudService<ComponentType, ComponentTypeRead, ComponentTypeCreate, ComponentTypeUpdate> service)
    : CrudController<ComponentType, ComponentTypeRead, ComponentTypeCreate, ComponentTypeUpdate>(service, "ComponentType", "ComponentTypes")
{
    protected override string WritePermission => Permissions.DashboardManage;
}

public class DashboardWidgetController(ICrudService<DashboardWidget, DashboardWidgetRead, DashboardWidgetCreate, DashboardWidgetUpdate> service)
    : CrudController<DashboardWidget, DashboardWidgetRead, DashboardWidgetCreate, DashboardWidgetUpdate>(service, "DashboardWidget", "DashboardWidgets")
{
    protected override string WritePermission => Permissions.DashboardManage;

    protected override string? ReadPermission => Permissions.DashboardManage;
}

public class DashboardLayoutController(ICrudService<DashboardLayout, DashboardLayoutRead, DashboardLayoutCreate, DashboardLayoutUpdate> service)
    : CrudController<DashboardLayout, DashboardLayoutRead, DashboardLayoutCreate, DashboardLayoutUpdate>(service, "DashboardLayout", "DashboardLayouts")
{
    protected override string WritePermission => Permissions.DashboardManage;

    protected override string? ReadPermission => Permissions.DashboardManage;
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController(IDashboardService dashboards) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<DashboardView>>> Mine(CancellationToken cancellationToken)
        => Ok(ApiResponse<DashboardView>.Success(await dashboards.MineAsync(cancellationToken), "Dashboard retrieved"));

    [HttpPut("mine")]
    public async Task<ActionResult<ApiResponse<DashboardView>>> Save([FromBody] DashboardLayoutWrite layout, CancellationToken cancellationToken)
        => Ok(ApiResponse<DashboardView>.Success(await dashboards.SaveMineAsync(layout, cancellationToken), "Dashboard saved"));

    [HttpDelete("mine")]
    public async Task<ActionResult<ApiResponse<bool>>> Reset(CancellationToken cancellationToken)
    {
        await dashboards.ResetMineAsync(cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Dashboard reset"));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SettingsController(ISettingsService settings) : ControllerBase
{
    [HttpGet("effective")]
    public async Task<ActionResult<ApiResponse<List<SettingValue>>>> Effective([FromQuery] string keys, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<SettingValue>>.Success(
            await settings.EffectiveAsync((keys ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries), cancellationToken),
            "Settings retrieved"));

    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<List<SettingValue>>>> Mine(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<SettingValue>>.Success(await settings.MineAsync(cancellationToken), "Settings retrieved"));

    [HttpPut("mine/{key}")]
    public async Task<ActionResult<ApiResponse<SettingValue>>> Set(string key, [FromBody] UserSettingWrite body, CancellationToken cancellationToken)
        => Ok(ApiResponse<SettingValue>.Success(await settings.SetMineAsync(key, body.Value, cancellationToken), "Setting saved"));

    [HttpDelete("mine/{key}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(string key, CancellationToken cancellationToken)
        => Ok(ApiResponse<bool>.Success(await settings.DeleteMineAsync(key, cancellationToken), "Setting removed"));
}

public class UnitRules(IUnitOfWork uow, IMultiTenantAccessor current) : CrudRules<Unit, UnitCreate, UnitUpdate>
{
    public override async Task OnCreateAsync(UnitCreate create, Unit entity, CancellationToken cancellationToken)
    {
        if (current.CompanyId is null)
            throw new BadRequestException("Choose a company first: a unit belongs to the company you are working in");

        entity.IdentityCompanyId = current.CompanyId;
        await EnsureManagerIsAMemberAsync(create.ManagerUserId, cancellationToken);
    }

    public override async Task OnUpdateAsync(Unit entity, UnitUpdate update, CancellationToken cancellationToken)
    {
        await EnsureManagerIsAMemberAsync(update.ManagerUserId, cancellationToken);

        if (update.ParentId is not { } parentId)
            return;
        if (parentId == entity.Id)
            throw new BadRequestException("A unit cannot be its own parent");

        var seen = new HashSet<long>();
        for (long? at = parentId; at is { } step && seen.Add(step); )
        {
            if (step == entity.Id)
                throw new BadRequestException("A unit cannot be moved under one of its own children");
            at = await uow.Repository<Unit>().Query().Where(u => u.Id == step).Select(u => u.ParentId).FirstOrDefaultAsync(cancellationToken);
        }
    }

    private async Task EnsureManagerIsAMemberAsync(long? managerId, CancellationToken cancellationToken)
    {
        if (managerId is not { } id || current.CompanyId is not { } companyId)
            return;

        var member = await uow.Repository<FSIdentity.Domain.Entities.Accounts.UserCompany>().Query()
            .AnyAsync(m => m.UserId == id && m.CompanyId == companyId && m.IsActive, cancellationToken);
        if (!member)
            throw new BadRequestException($"User {id} is not a member of this company");
    }
}

public class UserUnitRules(IUnitOfWork uow, IMultiTenantAccessor current) : CrudRules<UserUnit, UserUnitCreate, UserUnitUpdate>
{
    public override async Task OnCreateAsync(UserUnitCreate create, UserUnit entity, CancellationToken cancellationToken)
    {
        if (current.CompanyId is null)
            throw new BadRequestException("Choose a company first");

        entity.IdentityCompanyId = current.CompanyId;
        await EnsureMemberAsync(create.UserId, cancellationToken);
        if (create.IsPrimary)
            await ClearPrimaryAsync(create.UserId, null, cancellationToken);
    }

    public override async Task OnUpdateAsync(UserUnit entity, UserUnitUpdate update, CancellationToken cancellationToken)
    {
        await EnsureMemberAsync(update.UserId, cancellationToken);
        if (update.IsPrimary && !entity.IsPrimary)
            await ClearPrimaryAsync(update.UserId, entity.Id, cancellationToken);
    }

    public override Task OnRestoreAsync(UserUnit entity, CancellationToken cancellationToken)
    {
        entity.IsPrimary = false;
        return Task.CompletedTask;
    }

    private async Task EnsureMemberAsync(long userId, CancellationToken cancellationToken)
    {
        var member = await uow.Repository<FSIdentity.Domain.Entities.Accounts.UserCompany>().Query()
            .AnyAsync(m => m.UserId == userId && m.CompanyId == current.CompanyId && m.IsActive, cancellationToken);
        if (!member)
            throw new BadRequestException($"User {userId} is not a member of this company");
    }

    private async Task ClearPrimaryAsync(long userId, long? exceptId, CancellationToken cancellationToken)
    {
        foreach (var other in await uow.Repository<UserUnit>().Query()
                     .Where(u => u.UserId == userId && u.IsPrimary && u.Id != exceptId).ToListAsync(cancellationToken))
            other.IsPrimary = false;
    }
}

public class DashboardLayoutRules(IUnitOfWork uow, IDashboardService dashboards, Microsoft.AspNetCore.Http.IHttpContextAccessor http) : CrudRules<DashboardLayout, DashboardLayoutCreate, DashboardLayoutUpdate>
{
    public override Task OnCreateAsync(DashboardLayoutCreate create, DashboardLayout entity, CancellationToken cancellationToken)
        => CheckAsync(create, entity, cancellationToken);

    public override Task OnUpdateAsync(DashboardLayout entity, DashboardLayoutUpdate update, CancellationToken cancellationToken)
        => CheckAsync(update, entity, cancellationToken);

    private async Task CheckAsync(DashboardLayoutCreate layout, DashboardLayout entity, CancellationToken cancellationToken)
    {
        var items = DashboardLayoutCodec.Read(layout.LayoutJson);
        DashboardLayoutCodec.Validate(items);
        await dashboards.EnsureWidgetsExistAsync(items, cancellationToken);
        entity.LayoutJson = DashboardLayoutCodec.Write(items);

        var exists = layout.Scope switch
        {
            DashboardScope.Application => await uow.Repository<ApplicationType>().Query().AnyAsync(a => a.Id == layout.ScopeId, cancellationToken),
            DashboardScope.CompanyType => await uow.Repository<CompanyType>().Query().AnyAsync(t => t.Id == layout.ScopeId, cancellationToken),
            DashboardScope.Company => await uow.Repository<Company>().Query().AnyAsync(c => c.Id == layout.ScopeId, cancellationToken),
            DashboardScope.User => await uow.Repository<FSIdentity.Domain.Entities.Accounts.User>().Query().AnyAsync(u => u.Id == layout.ScopeId, cancellationToken),
            DashboardScope.Tenant => await uow.Repository<Tenant>().Query().AnyAsync(t => t.Id == layout.ScopeId, cancellationToken),
            DashboardScope.Role => await uow.Repository<FSIdentity.Domain.Entities.Accounts.Role>().Query().AnyAsync(r => r.Id == layout.ScopeId, cancellationToken),
            DashboardScope.UserType => Enum.IsDefined(typeof(FSIdentity.Domain.Entities.Accounts.UserType), (int)layout.ScopeId)
                                       && layout.ScopeId < (int)FSIdentity.Domain.Entities.Accounts.UserType.PlatformAdmin,
            _ => false,
        };
        if (!exists)
            throw new BadRequestException($"{layout.Scope} {layout.ScopeId} does not exist");

        if (layout.CompanyId is not null && layout.Scope is not (DashboardScope.Role or DashboardScope.UserType))
            throw new BadRequestException("Only a role or rank layout can be limited to a company");
        if (layout.CompanyId is { } companyId && !await uow.Repository<Company>().Query().AnyAsync(c => c.Id == companyId, cancellationToken))
            throw new BadRequestException($"Company {companyId} does not exist");
        if (layout.ApplicationTypeId is { } applicationId && !await uow.Repository<ApplicationType>().Query().AnyAsync(a => a.Id == applicationId, cancellationToken))
            throw new BadRequestException($"ApplicationType {applicationId} does not exist");
        if (layout.Scope == DashboardScope.Application && layout.ApplicationTypeId is { } scoped && scoped != layout.ScopeId)
            throw new BadRequestException("An application's layout cannot be limited to another application");

        entity.IdentityTenantId = await OwnerTenantAsync(layout, entity.IdentityTenantId, cancellationToken);
    }

    private async Task<long?> OwnerTenantAsync(DashboardLayoutCreate layout, long? current, CancellationToken cancellationToken)
    {
        var platformAdmin = http.HttpContext?.User.IsSuperAdmin() == true;

        return layout.Scope switch
        {
            DashboardScope.Application or DashboardScope.CompanyType => platformAdmin ? null : current,
            DashboardScope.Tenant => layout.ScopeId,
            DashboardScope.Company => await uow.Repository<Company>().Query().Where(c => c.Id == layout.ScopeId).Select(c => c.IdentityTenantId).FirstOrDefaultAsync(cancellationToken),
            DashboardScope.Role => await uow.Repository<FSIdentity.Domain.Entities.Accounts.Role>().Query().Where(r => r.Id == layout.ScopeId).Select(r => r.IdentityTenantId).FirstOrDefaultAsync(cancellationToken),
            DashboardScope.User => await uow.Repository<FSIdentity.Domain.Entities.Accounts.User>().Query().Where(u => u.Id == layout.ScopeId).Select(u => u.IdentityTenantId).FirstOrDefaultAsync(cancellationToken),
            DashboardScope.UserType when layout.CompanyId is { } companyId => await uow.Repository<Company>().Query().Where(c => c.Id == companyId).Select(c => c.IdentityTenantId).FirstOrDefaultAsync(cancellationToken),
            _ => current,
        };
    }
}

public class PlatformDashboardLayoutHook(Microsoft.AspNetCore.Http.IHttpContextAccessor http) : IPostCreateHook<DashboardLayout>
{
    public Task AfterCreateAsync(DashboardLayout layout, CancellationToken cancellationToken)
    {
        if (layout.Scope is DashboardScope.Application or DashboardScope.CompanyType && http.HttpContext?.User.IsSuperAdmin() == true)
            layout.IdentityTenantId = null;
        return Task.CompletedTask;
    }
}

public class TenantBaselineHook(IUnitOfWork uow) : IPostCreateHook<Tenant>
{
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["TIME_ZONE"] = "UTC",
        ["DATE_FORMAT"] = "yyyy-MM-dd",
    };

    public async Task AfterCreateAsync(Tenant tenant, CancellationToken cancellationToken)
    {
        foreach (var (key, value) in Defaults)
            await uow.Repository<TenantSetting>().AddAsync(
                new TenantSetting { Key = key, Value = value, IdentityTenantId = tenant.Id }, cancellationToken);
    }
}

public class CompanyBaselineHook(IUnitOfWork uow) : IPostCreateHook<Company>
{
    public static readonly IReadOnlyDictionary<string, string> Defaults = new Dictionary<string, string>
    {
        ["WEEK_START"] = "Monday",
    };

    public async Task AfterCreateAsync(Company company, CancellationToken cancellationToken)
    {
        foreach (var (key, value) in Defaults)
            await uow.Repository<CompanySetting>().AddAsync(
                new CompanySetting { Key = key, Value = value, IdentityTenantId = company.IdentityTenantId, IdentityCompanyId = company.Id },
                cancellationToken);
    }
}

public class OrganizationUnit
{
    public long Id { get; init; }
    public long? ParentId { get; init; }
    public long? CompanyId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long UnitTypeId { get; init; }
    public long? ManagerUserId { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

[ApiController]
[Route("api/[controller]")]
[RequirePermission(Permissions.DashboardManage)]
public class DashboardStudioController(IDashboardService dashboards) : ControllerBase
{
    [HttpGet("preview")]
    public async Task<ActionResult<ApiResponse<DashboardView>>> Preview(
        [FromQuery] long applicationTypeId, [FromQuery] long? companyId, [FromQuery] long? roleId,
        [FromQuery] FSIdentity.Domain.Entities.Accounts.UserType? userType, CancellationToken cancellationToken)
        => Ok(ApiResponse<DashboardView>.Success(await dashboards.PreviewAsync(applicationTypeId, companyId, roleId, userType, cancellationToken), "Dashboard preview retrieved"));
}
