using FSIdentity.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace FSIdentity.Api.Startup;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var body = ExceptionMapper.Map(exception);
        body.TraceId = httpContext.TraceIdentifier;

        if (body.StatusCode >= 500)
        {
            logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}, Path: {Path}",
                body.TraceId, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Handled exception ({Code}). TraceId: {TraceId}, Path: {Path}",
                body.StatusCode, body.TraceId, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = body.StatusCode;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }
}