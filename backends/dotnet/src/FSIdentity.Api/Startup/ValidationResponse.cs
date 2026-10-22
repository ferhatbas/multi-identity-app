using FSIdentity.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FSIdentity.Api.Startup;

public static class ValidationResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var failed = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToList();

        var hasSpecificError = failed.Any(entry => entry.Key != "body");

        var errors = failed
            .Where(entry => !(hasSpecificError && entry.Key == "body"))
            .SelectMany(entry => entry.Value!.Errors.Select(
                error => ApiError.ValidationError(ToFieldName(entry.Key), error.ErrorMessage)))
            .ToList();

        var body = ApiResponse<object>.BadRequest("Invalid input data", errors);
        body.TraceId = context.HttpContext.TraceIdentifier;

        return new ObjectResult(body) { StatusCode = StatusCodes.Status400BadRequest };
    }

    internal static string ToFieldName(string key)
    {
        var path = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key == "$" ? string.Empty : key;
        return string.Join('.', path.Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => char.ToLowerInvariant(segment[0]) + segment[1..]));
    }
}