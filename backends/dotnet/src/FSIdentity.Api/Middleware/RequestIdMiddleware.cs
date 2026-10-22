namespace FSIdentity.Api.Middleware;

public class RequestIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Request-ID";
    private const int MaxLength = 64;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        var requestId = IsValid(incoming) ? incoming! : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = requestId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = requestId;
            return Task.CompletedTask;
        });

        await next(context);
    }

    internal static bool IsValid(string? value)
        => !string.IsNullOrEmpty(value)
           && value.Length <= MaxLength
           && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
}

public static class RequestIdMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestId(this IApplicationBuilder app)
        => app.UseMiddleware<RequestIdMiddleware>();
}