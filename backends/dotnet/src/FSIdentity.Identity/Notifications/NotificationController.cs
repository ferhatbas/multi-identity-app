using FSIdentity.Domain.Common;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity.Notifications;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController(INotificationService notifications) : ControllerBase
{
    [HttpGet("preferences")]
    public async Task<ActionResult<ApiResponse<List<PreferenceView>>>> Preferences(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<PreferenceView>>.Success(await notifications.PreferencesAsync(cancellationToken), "Preferences retrieved"));

    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<List<PreferenceView>>>> SavePreferences([FromBody] PreferencesWrite body, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<PreferenceView>>.Success(await notifications.SavePreferencesAsync(body.Items, cancellationToken), "Preferences saved"));

    [HttpGet("push-tokens")]
    public async Task<ActionResult<ApiResponse<List<PushTokenView>>>> Tokens(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<PushTokenView>>.Success(await notifications.TokensAsync(cancellationToken), "Devices retrieved"));

    [HttpPost("push-tokens")]
    [NotWhileImpersonating]
    public async Task<ActionResult<ApiResponse<PushTokenView>>> Register([FromBody] PushTokenRegister body, CancellationToken cancellationToken)
        => Ok(ApiResponse<PushTokenView>.Success(await notifications.RegisterTokenAsync(body, cancellationToken), "Device registered"));

    [HttpDelete("push-tokens/{id:long}")]
    public async Task<ActionResult<ApiResponse<bool>>> Remove(long id, CancellationToken cancellationToken)
        => Ok(ApiResponse<bool>.Success(await notifications.RemoveTokenAsync(id, cancellationToken), "Device removed"));

    [HttpGet("push-tickets")]
    public async Task<ActionResult<ApiResponse<List<PushTicketView>>>> Tickets(CancellationToken cancellationToken)
        => Ok(ApiResponse<List<PushTicketView>>.Success(await notifications.TicketsAsync(cancellationToken), "Pushes retrieved"));

    [HttpPost("push-test")]
    public async Task<ActionResult<ApiResponse<List<PushTicketView>>>> Test([FromBody] PushRequest body, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<PushTicketView>>.Success(await notifications.SendTestAsync(body, cancellationToken), "Push attempted"));
}
