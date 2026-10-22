using System.Text.Json.Serialization;
using FSIdentity.Domain.Enums;

namespace FSIdentity.Domain.Common;

public class ApiResponse<T> : IApiResponse
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public ApiResponseTypes Types { get; init; }
    public IReadOnlyList<ApiError> Errors { get; init; } = [];
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? TraceId { get; set; }

    [JsonIgnore]
    public bool IsSuccess => Types == ApiResponseTypes.SUCCESS;

    [JsonIgnore]
    public int StatusCode => int.TryParse(Code, out var code) ? code : 500;

    public static ApiResponse<T> Success(T data, string message = "Success")
        => new() { Code = "200", Message = message, Data = data, Types = ApiResponseTypes.SUCCESS };

    public static ApiResponse<T> Created(T data, string message = "Resource created")
        => new() { Code = "201", Message = message, Data = data, Types = ApiResponseTypes.SUCCESS };

    public static ApiResponse<T> Accepted(T data, string message = "Request accepted")
        => new() { Code = "202", Message = message, Data = data, Types = ApiResponseTypes.SUCCESS };

    public static ApiResponse<T> Failure(int statusCode, string message, IEnumerable<ApiError>? errors = null)
        => new()
        {
            Code = statusCode.ToString(),
            Message = message,
            Errors = errors?.ToList() ?? [],
            Types = statusCode >= 500 ? ApiResponseTypes.SERVER_ERROR : ApiResponseTypes.CLIENT_ERROR,
        };

    public static ApiResponse<T> BadRequest(string message, IEnumerable<ApiError>? errors = null)
        => Failure(400, message, errors);

    public static ApiResponse<T> Unauthorized(string message = "Unauthorized")
        => Failure(401, message);

    public static ApiResponse<T> Forbidden(string message = "Forbidden")
        => Failure(403, message);

    public static ApiResponse<T> NotFound(string message = "Resource not found")
        => Failure(404, message);

    public static ApiResponse<T> Conflict(string message = "Conflict occurred")
        => Failure(409, message);

    public static ApiResponse<T> ValidationError(IEnumerable<ApiError> errors, string message = "Validation failed")
        => Failure(422, message, errors);

    public static ApiResponse<T> InternalError(string message = "An unexpected error occurred")
        => Failure(500, message);

    public static ApiResponse<T> NotImplemented(string message = "Feature not implemented")
        => Failure(501, message);

    public static ApiResponse<T> ServiceUnavailable(string message = "Service temporarily unavailable")
        => Failure(503, message);

    public ApiResponse<TOther> AsErrorResponse<TOther>() => new()
    {
        Code = Code,
        Message = Message,
        Errors = Errors,
        Types = Types,
        TraceId = TraceId,
    };
}