using FSIdentity.Domain.Common;
using FSIdentity.Domain.Modules;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Identity.Applications;

public record ModuleScopeResult(long? ApplicationTypeId, bool Configured, IReadOnlyList<string> Modules);

[ApiController]
[Route("api/[controller]")]
public class ModuleScopeController(IModuleScopeAccessor scope, IApplicationScopeService applications) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<ApiResponse<ModuleScopeResult>>> GetMine(CancellationToken cancellationToken)
    {
        long? onToken = long.TryParse(User.FindFirst(ClaimNames.ActiveApplicationTypeId)?.Value, out var appId) ? appId : null;
        long? companyId = long.TryParse(User.FindFirst(ClaimNames.ActiveCompanyId)?.Value, out var id) ? id : null;

        var applicationTypeId = companyId is null
            ? null
            : await applications.ResolveForSessionAsync(companyId.Value, onToken, cancellationToken: cancellationToken);

        var modules = await scope.GetGrantedCodesAsync(cancellationToken);

        var configured = await applications.AnyModuleGrantsConfiguredAsync(cancellationToken);

        return Ok(ApiResponse<ModuleScopeResult>.Success(
            new ModuleScopeResult(applicationTypeId, configured, modules.OrderBy(c => c, StringComparer.Ordinal).ToList()),
            "Module scope retrieved successfully"));
    }
}