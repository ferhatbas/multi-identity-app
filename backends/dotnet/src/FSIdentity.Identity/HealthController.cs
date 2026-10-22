using FSIdentity.Domain.Common;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity;

public record HealthStatus(string Status, DateTime Time);

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public ActionResult<ApiResponse<HealthStatus>> Live()
        => Ok(ApiResponse<HealthStatus>.Success(new HealthStatus("Healthy", clock.GetUtcNow().UtcDateTime), "Healthy"));

    [HttpGet("ready")]
    public async Task<ActionResult<ApiResponse<HealthStatus>>> Ready(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        if (await db.Database.CanConnectAsync(cancellationToken))
            return Ok(ApiResponse<HealthStatus>.Success(new HealthStatus("Ready", now), "Ready"));

        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            ApiResponse<HealthStatus>.Failure(503, "The database is not reachable", [new ApiError("UNAVAILABLE", "The database is not reachable")]));
    }
}
