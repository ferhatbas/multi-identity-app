using System.Diagnostics;

namespace FSIdentity.Api.Middleware;

public class AccessLogMiddleware(RequestDelegate next, ILogger<AccessLogMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var status = 500;

        try
        {
            await next(context);
            status = context.Response.StatusCode;
        }
        finally
        {
            logger.LogInformation(
                "{Method} {Path} -> {Status} {ElapsedMs:0.0}ms rid={RequestId}",
                context.Request.Method,
                context.Request.Path,
                status,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                context.TraceIdentifier);
        }
    }
}

public static class AccessLogMiddlewareExtensions
{
    public static IApplicationBuilder UseAccessLog(this IApplicationBuilder app)
        => app.UseMiddleware<AccessLogMiddleware>();
}