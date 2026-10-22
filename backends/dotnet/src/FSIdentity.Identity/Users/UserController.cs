using FSIdentity.Domain.Common;
using FSIdentity.Domain.Controllers;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity.Users;

public class UserController(
    ICrudService<User, UserRead, UserCreate, UserUpdate> service,
    IAccessManagementService access)
    : CrudController<User, UserRead, UserCreate, UserUpdate>(service, "User", "Users")
{
    protected override string WritePermission => Permissions.UserManage;

    protected override string? ReadPermission => Permissions.UserView;

    [HttpGet("{id:long}/effective-permissions")]
    public async Task<ActionResult<ApiResponse<EffectivePermissionsRead>>> EffectivePermissions(
        long id, [FromQuery] long? companyId, CancellationToken cancellationToken)
        => Ok(ApiResponse<EffectivePermissionsRead>.Success(
            await access.EffectivePermissionsAsync(id, companyId, cancellationToken), "Effective permissions retrieved"));

    [HttpPost("{id:long}/copy")]
    public async Task<ActionResult<ApiResponse<CopiedUser>>> Copy(
        long id, [FromBody] UserCopyRequest request, CancellationToken cancellationToken)
        => StatusCode(StatusCodes.Status201Created,
            ApiResponse<CopiedUser>.Created(await access.CopyUserAsync(id, request, cancellationToken), "User copied successfully"));

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<BulkUserImportResult>>> Import(
        [FromBody] BulkUserImportRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<BulkUserImportResult>.Success(
            await access.ImportUsersAsync(request, cancellationToken), "Users imported"));
}
