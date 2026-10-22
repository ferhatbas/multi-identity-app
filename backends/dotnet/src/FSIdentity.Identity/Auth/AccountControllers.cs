using FSIdentity.Domain.Common;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity.Auth;

[ApiController]
[Route("api/Auth")]
public class AccountRecoveryController(IAccountRecoveryService recovery) : ControllerBase
{
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await recovery.RequestPasswordResetAsync(request.Email, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "If the address belongs to an account, an e-mail is on its way"));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await recovery.ResetPasswordAsync(request, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Password reset"));
    }

    [HttpPost("send-confirmation")]
    [Authorize]
    [AccountSelfService]
    [AllowReader]
    public async Task<ActionResult<ApiResponse<bool>>> SendConfirmation(CancellationToken cancellationToken)
    {
        await recovery.SendEmailConfirmationAsync(User.UserId() ?? throw new UnauthorizedException(), cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Confirmation e-mail sent"));
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<bool>>> ConfirmEmail([FromBody] ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await recovery.ConfirmEmailAsync(request.Token, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "E-mail confirmed"));
    }
}

[ApiController]
[Route("api/Auth/2fa")]
[Authorize]
[AccountSelfService]
[AllowReader]
[NotWhileImpersonating]
public class TwoFactorController(ITwoFactorService twoFactor) : ControllerBase
{
    [HttpPost("setup")]
    public async Task<ActionResult<ApiResponse<TwoFactorSetup>>> Setup(CancellationToken cancellationToken)
        => Ok(ApiResponse<TwoFactorSetup>.Success(await twoFactor.SetupAsync(CurrentUser, cancellationToken), "Two-factor setup started"));

    [HttpPost("enable")]
    public async Task<ActionResult<ApiResponse<RecoveryCodes>>> Enable([FromBody] TwoFactorCodeRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RecoveryCodes>.Success(await twoFactor.EnableAsync(CurrentUser, request.Code, cancellationToken), "Two-factor authentication is on"));

    [HttpPost("disable")]
    public async Task<ActionResult<ApiResponse<bool>>> Disable([FromBody] TwoFactorConfirmRequest request, CancellationToken cancellationToken)
    {
        await twoFactor.DisableAsync(CurrentUser, request, cancellationToken);
        return Ok(ApiResponse<bool>.Success(true, "Two-factor authentication is off"));
    }

    [HttpPost("recovery-codes")]
    public async Task<ActionResult<ApiResponse<RecoveryCodes>>> RecoveryCodes([FromBody] TwoFactorConfirmRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<RecoveryCodes>.Success(await twoFactor.RegenerateRecoveryCodesAsync(CurrentUser, request, cancellationToken), "New recovery codes"));

    private long CurrentUser => User.UserId() ?? throw new UnauthorizedException();
}
