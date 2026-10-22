using FSIdentity.Domain.Common;

namespace FSIdentity.Domain.Exceptions;

public static class ExceptionMapper
{
    public static ApiResponse<object> Map(Exception exception)
    {
        var (status, code, message) = exception switch
        {
            AppException app => (app.StatusCode, app.ErrorCode, app.Message),
            KeyNotFoundException => (404, "NOT_FOUND", exception.Message),
            UnauthorizedAccessException => (401, "UNAUTHORIZED", "Unauthorized"),
            ArgumentException or InvalidOperationException => (400, "BAD_REQUEST", exception.Message),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred"),
        };

        return ApiResponse<object>.Failure(status, message, [new ApiError(code, message)]);
    }
}