using FSIdentity.Domain.Common;
using FSIdentity.Domain.Controllers;
using FSIdentity.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Security;
using FSIdentity.Domain.Entities.Accounts;

namespace FSIdentity.Identity.Accounts;

public class PermissionController(ICrudService<Permission, PermissionRead, PermissionCreate, PermissionUpdate> service)
    : CrudController<Permission, PermissionRead, PermissionCreate, PermissionUpdate>(service, "Permission", "Permissions")
{
    protected override string WritePermission => Permissions.PermissionManage;

    protected override string? ReadPermission => Permissions.RoleView;
}

public class RoleController(
    ICrudService<Role, RoleRead, RoleCreate, RoleUpdate> service,
    IAccessManagementService access)
    : CrudController<Role, RoleRead, RoleCreate, RoleUpdate>(service, "Role", "Roles")
{
    protected override string WritePermission => Permissions.RoleManage;

    protected override string? ReadPermission => Permissions.RoleView;

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<ApiResponse<RoleRead>>> Copy(
        long id, [FromBody] RoleCopyRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<RoleRead>.Created(await access.CopyRoleAsync(id, request, cancellationToken), "Role copied successfully"));
}

public class RolePermissionController(
    ICrudService<RolePermission, RolePermissionRead, RolePermissionCreate, RolePermissionUpdate> service)
    : CrudController<RolePermission, RolePermissionRead, RolePermissionCreate, RolePermissionUpdate>(
        service, "RolePermission", "RolePermissions")
{
    protected override string WritePermission => Permissions.RoleManage;

    protected override string? ReadPermission => Permissions.RoleView;
}

public class UserCompanyController(
    ICrudService<UserCompany, UserCompanyRead, UserCompanyCreate, UserCompanyUpdate> service)
    : CrudController<UserCompany, UserCompanyRead, UserCompanyCreate, UserCompanyUpdate>(
        service, "UserCompany", "UserCompanies")
{
    protected override string WritePermission => Permissions.UserManage;

    protected override string? ReadPermission => Permissions.UserView;
}

public class UserCompanyRoleController(
    ICrudService<UserCompanyRole, UserCompanyRoleRead, UserCompanyRoleCreate, UserCompanyRoleUpdate> service)
    : CrudController<UserCompanyRole, UserCompanyRoleRead, UserCompanyRoleCreate, UserCompanyRoleUpdate>(
        service, "UserCompanyRole", "UserCompanyRoles")
{
    protected override string WritePermission => Permissions.UserManage;

    protected override string? ReadPermission => Permissions.UserView;
}

public class UserCompanyPermissionController(
    ICrudService<UserCompanyPermission, UserCompanyPermissionRead, UserCompanyPermissionCreate, UserCompanyPermissionUpdate> service)
    : CrudController<UserCompanyPermission, UserCompanyPermissionRead, UserCompanyPermissionCreate, UserCompanyPermissionUpdate>(
        service, "UserCompanyPermission", "UserCompanyPermissions")
{
    protected override string WritePermission => Permissions.UserManage;

    protected override string? ReadPermission => Permissions.UserView;
}
