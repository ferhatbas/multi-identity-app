using FSIdentity.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace FSIdentity.Api.Startup;

public static class StatusCodeResponses
{
    public static async Task WriteAsync(StatusCodeContext context)
    {
        var http = context.HttpContext;
        var status = http.Response.StatusCode;
        var (code, message) = Describe(status);

        var body = ApiResponse<object>.Failure(status, message, [new ApiError(code, message)]);
        body.TraceId = http.TraceIdentifier;

        await http.Response.WriteAsJsonAsync(body, http.RequestAborted);
    }

    public static (string Code, string Message) Describe(int status) => status switch
    {
        400 => ("BAD_REQUEST", "Bad request"),
        401 => ("UNAUTHORIZED", "Unauthorized"),
        403 => ("FORBIDDEN", "Forbidden"),
        404 => ("NOT_FOUND", "Resource not found"),
        405 => ("METHOD_NOT_ALLOWED", "Method not allowed"),
        406 => ("NOT_ACCEPTABLE", "Not acceptable"),
        409 => ("CONFLICT", "Conflict occurred"),
        415 => ("UNSUPPORTED_MEDIA_TYPE", "Unsupported media type; send application/json"),
        429 => ("TOO_MANY_REQUESTS", "Too many requests"),
        >= 500 => ("INTERNAL_ERROR", "An unexpected error occurred"),
        _ => ("CLIENT_ERROR", "The request could not be processed"),
    };
}
