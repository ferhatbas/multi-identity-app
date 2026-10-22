using FSIdentity.Domain.Common;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity.Users;

[ApiController]
[Route("api/User")]
[Authorize]
public class UserAdminController(IUserAdminService admin) : ControllerBase
{
    [HttpPost("{id:long}/reset-password")]
    [RequirePermission(Permissions.UserManage)]
    [NotWhileImpersonating]
    public async Task<ActionResult<ApiResponse<TemporaryPassword>>> ResetPassword(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<TemporaryPassword>.Success(
            await admin.ResetPasswordAsync(id, User.IsSuperAdmin(), cancellationToken), "Password reset"));

    [HttpPost("{id:long}/unlock")]
    [RequirePermission(Permissions.UserManage)]
    public async Task<ActionResult<ApiResponse<bool>>> Unlock(long id, CancellationToken cancellationToken)
    {
        await admin.UnlockAsync(id, User.IsSuperAdmin(), cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Account unlocked"));
    }

    [HttpGet("{id:long}/sessions")]
    [RequirePermission(Permissions.SessionManage)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<SessionRead>>>> Sessions(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<SessionRead>>.Success(await admin.SessionsAsync(id, cancellationToken), "Sessions retrieved"));

    [HttpPost("{id:long}/sessions/revoke")]
    [RequirePermission(Permissions.SessionManage)]
    public async Task<ActionResult<ApiResponse<int>>> RevokeAll(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<int>.Success(
            await admin.RevokeSessionsAsync(id, null, User.IsSuperAdmin(), cancellationToken), "Sessions ended"));

    [HttpPost("{id:long}/sessions/{sessionId:long}/revoke")]
    [RequirePermission(Permissions.SessionManage)]
    public async Task<ActionResult<ApiResponse<int>>> RevokeOne(long id, long sessionId, CancellationToken cancellationToken)
        => Ok(ApiResponse<int>.Success(
            await admin.RevokeSessionsAsync(id, sessionId, User.IsSuperAdmin(), cancellationToken), "Session ended"));
}
