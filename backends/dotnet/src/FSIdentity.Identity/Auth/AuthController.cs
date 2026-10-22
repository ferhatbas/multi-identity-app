using FSIdentity.Domain.Common;
using FSIdentity.Identity.Users;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity.Auth;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService auth, IUserAdminService admin) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AuthResponse>.Success(await auth.LoginAsync(request, cancellationToken), "Signed in"));

    [HttpPost("login/2fa")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> LoginWithTwoFactor(
        [FromBody] TwoFactorLoginRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AuthResponse>.Success(await auth.LoginWithTwoFactorAsync(request, cancellationToken), "Signed in"));

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AuthResponse>.Success(await auth.RefreshAsync(request.RefreshToken, cancellationToken), "Token refreshed"));

    [HttpPost("logout")]
    [Authorize]
    [AllowReader]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<bool>>> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(Session, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Signed out"));
    }

    [HttpGet("me")]
    [Authorize]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<MeResponse>>> Me(CancellationToken cancellationToken)
        => Ok(ApiResponse<MeResponse>.Success(await auth.MeAsync(CurrentUser, Session, cancellationToken), "Current user retrieved"));

    [HttpPost("change-company")]
    [Authorize]
    [AllowReader]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> ChangeCompany(
        [FromBody] ChangeCompanyRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AuthResponse>.Success(
            await auth.ChangeCompanyAsync(CurrentUser, Session, request.CompanyId, cancellationToken), "Company changed"));

    [HttpPost("change-application")]
    [Authorize]
    [AllowReader]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> ChangeApplication(
        [FromBody] ChangeApplicationRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AuthResponse>.Success(
            await auth.ChangeApplicationAsync(CurrentUser, Session, request.ApplicationTypeId, cancellationToken), "Application changed"));

    [HttpGet("sessions")]
    [Authorize]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<List<SessionRead>>>> Sessions(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<SessionRead>>.Success(await admin.ListAsync(CurrentUser, Session, cancellationToken), "Sessions retrieved"));

    [HttpPost("sessions/{sessionId:long}/revoke")]
    [Authorize]
    [AccountSelfService]
    [AllowReader]
    public async Task<ActionResult<ApiResponse<bool>>> RevokeSession(long sessionId, CancellationToken cancellationToken)
    {
        await auth.RevokeOwnSessionAsync(CurrentUser, sessionId, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Session ended"));
    }

    [HttpPost("logout-all")]
    [Authorize]
    [AllowReader]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<bool>>> LogoutAll(CancellationToken cancellationToken)
    {
        await auth.LogoutEverywhereAsync(CurrentUser, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Signed out everywhere"));
    }

    [HttpPost("impersonate")]
    [RequirePermission(Permissions.Impersonate)]
    [NotWhileImpersonating]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Impersonate(
        [FromBody] ImpersonateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<AuthResponse>.Success(
            await auth.ImpersonateAsync(CurrentUser, User.IsSuperAdmin(), request.UserId, cancellationToken, request.Reason), "Acting as the user"));

    [HttpPost("change-password")]
    [Authorize]
    [AllowReader]
    [NotWhileImpersonating]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await auth.ChangePasswordAsync(CurrentUser, Session, request, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Password changed"));
    }

    [HttpPut("me/language")]
    [Authorize]
    [AllowReader]
    [AccountSelfService]
    public async Task<ActionResult<ApiResponse<MyLanguage>>> SetLanguage(
        [FromBody] SetLanguageRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MyLanguage>.Success(await auth.SetLanguageAsync(CurrentUser, Session, request.LanguageCode, cancellationToken), "Language saved"));

    private long CurrentUser => User.UserId() ?? throw new UnauthorizedException();

    private long Session => User.SessionId() ?? throw new UnauthorizedException("The token has no session");
}
