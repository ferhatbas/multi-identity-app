using System.Security.Claims;
using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Security;
using Microsoft.AspNetCore.Http;

namespace FSIdentity.Domain.Services;

public class HttpAuditContext(IHttpContextAccessor http) : IAuditContext
{
    public string? UserName
    {
        get
        {
            var user = http.HttpContext?.User;
            return user?.FindFirst(ClaimNames.UserName)?.Value
                   ?? user?.FindFirst(ClaimTypes.Name)?.Value
                   ?? user?.Identity?.Name;
        }
    }

    public long? ImpersonatorId => http.HttpContext?.User.ImpersonatorId();

    public string? IpAddress => http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => http.HttpContext?.Request.Headers.UserAgent.FirstOrDefault();

    public string? CorrelationId
        => http.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
           ?? http.HttpContext?.TraceIdentifier;
}
