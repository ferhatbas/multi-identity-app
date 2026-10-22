using FSIdentity.Domain.Common;
using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Menus;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Menus;

public class MenuTypeController(ICrudService<MenuType, MenuTypeRead, MenuTypeCreate, MenuTypeUpdate> service)
    : CrudController<MenuType, MenuTypeRead, MenuTypeCreate, MenuTypeUpdate>(service, "MenuType", "MenuTypes")
{
    protected override string WritePermission => Permissions.MenuDesign;

    protected override string? ReadPermission => Permissions.MenuDesign;
}

public class MenuController(ICrudService<Menu, MenuRead, MenuCreate, MenuUpdate> service)
    : CrudController<Menu, MenuRead, MenuCreate, MenuUpdate>(service, "Menu", "Menus")
{
    protected override string WritePermission => Permissions.MenuDesign;

    protected override string? ReadPermission => Permissions.MenuDesign;
}

public class MenuLocalizationController(
    ICrudService<MenuLocalization, MenuLocalizationRead, MenuLocalizationCreate, MenuLocalizationUpdate> service)
    : CrudController<MenuLocalization, MenuLocalizationRead, MenuLocalizationCreate, MenuLocalizationUpdate>(
        service, "MenuLocalization", "MenuLocalizations")
{
    protected override string WritePermission => Permissions.MenuDesign;

    protected override string? ReadPermission => Permissions.MenuDesign;
}

public class MenuCompanyTypeController(
    ICrudService<MenuCompanyType, MenuCompanyTypeRead, MenuCompanyTypeCreate, MenuCompanyTypeUpdate> service)
    : CrudController<MenuCompanyType, MenuCompanyTypeRead, MenuCompanyTypeCreate, MenuCompanyTypeUpdate>(
        service, "MenuCompanyType", "MenuCompanyTypes")
{
    protected override string WritePermission => Permissions.MenuDesign;

    protected override string? ReadPermission => Permissions.MenuDesign;
}

public class MenuApplicationTypeLayoutController(
    ICrudService<MenuApplicationTypeLayout, MenuApplicationTypeLayoutRead, MenuApplicationTypeLayoutCreate, MenuApplicationTypeLayoutUpdate> service)
    : CrudController<MenuApplicationTypeLayout, MenuApplicationTypeLayoutRead, MenuApplicationTypeLayoutCreate, MenuApplicationTypeLayoutUpdate>(
        service, "MenuApplicationTypeLayout", "MenuApplicationTypeLayouts")
{
    protected override string WritePermission => Permissions.MenuDesign;

    protected override string? ReadPermission => Permissions.MenuDesign;
}

public class MenuCompanyTypeLayoutController(
    ICrudService<MenuCompanyTypeLayout, MenuCompanyTypeLayoutRead, MenuCompanyTypeLayoutCreate, MenuCompanyTypeLayoutUpdate> service)
    : CrudController<MenuCompanyTypeLayout, MenuCompanyTypeLayoutRead, MenuCompanyTypeLayoutCreate, MenuCompanyTypeLayoutUpdate>(
        service, "MenuCompanyTypeLayout", "MenuCompanyTypeLayouts")
{
    protected override string WritePermission => Permissions.MenuDesign;

    protected override string? ReadPermission => Permissions.MenuDesign;
}

public class MenuPermissionOverrideController(
    ICrudService<MenuPermissionOverride, MenuPermissionOverrideRead, MenuPermissionOverrideCreate, MenuPermissionOverrideUpdate> service)
    : CrudController<MenuPermissionOverride, MenuPermissionOverrideRead, MenuPermissionOverrideCreate, MenuPermissionOverrideUpdate>(
        service, "MenuPermissionOverride", "MenuPermissionOverrides")
{
    protected override string WritePermission => Permissions.MenuManage;

    protected override string? ReadPermission => Permissions.MenuManage;
}

public class MenuFeatureToggleController(
    ICrudService<MenuFeatureToggle, MenuFeatureToggleRead, MenuFeatureToggleCreate, MenuFeatureToggleUpdate> service)
    : CrudController<MenuFeatureToggle, MenuFeatureToggleRead, MenuFeatureToggleCreate, MenuFeatureToggleUpdate>(
        service, "MenuFeatureToggle", "MenuFeatureToggles")
{
    protected override string WritePermission => Permissions.MenuManage;

    protected override string? ReadPermission => Permissions.MenuManage;
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NavigationController(INavigationService navigation) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<List<MenuNode>>>> Mine(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<MenuNode>>.Success(await navigation.MineAsync(cancellationToken), "Navigation retrieved"));

    [HttpGet("tree")]
    [RequirePermission(Permissions.MenuDesign)]
    public async Task<ActionResult<ApiResponse<List<MenuNode>>>> Tree(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<MenuNode>>.Success(await navigation.FullTreeAsync(cancellationToken), "Menu tree retrieved"));
}

public class MenuRules(IUnitOfWork uow) : CrudRules<Menu, MenuCreate, MenuUpdate>
{
    public override Task OnCreateAsync(MenuCreate create, Menu entity, CancellationToken cancellationToken)
        => CheckAsync(null, create, cancellationToken);

    public override Task OnUpdateAsync(Menu entity, MenuUpdate update, CancellationToken cancellationToken)
        => CheckAsync(entity.Id, update, cancellationToken);

    private async Task CheckAsync(long? ownId, MenuCreate menu, CancellationToken cancellationToken)
    {
        if (menu.ParentId is not { } parentId)
            return;

        if (parentId == ownId)
            throw new BadRequestException("An entry cannot be its own parent");

        if (ownId is { } id)
        {
            var seen = new HashSet<long>();
            for (long? current = parentId; current is { } step && seen.Add(step); )
            {
                if (step == id)
                    throw new BadRequestException("An entry cannot be moved under one of its own children");
                current = await uow.Repository<Menu>().Query().Where(m => m.Id == step).Select(m => m.ParentId).FirstOrDefaultAsync(cancellationToken);
            }
        }

        var parent = await uow.Repository<Menu>().Query()
            .Where(m => m.Id == parentId).Select(m => new { m.ApplicationTypeId }).FirstOrDefaultAsync(cancellationToken);
        if (parent?.ApplicationTypeId is { } parentScope && menu.ApplicationTypeId is { } scope && scope != parentScope)
            throw new BadRequestException($"The entry's application ({scope}) contradicts its parent's ({parentScope})");
    }
}
