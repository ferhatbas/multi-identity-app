namespace FSIdentity.Domain.Common;

public class ApiError
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Field { get; set; }
    public string? StackTrace { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }

    public ApiError()
    {
    }

    public ApiError(string code, string message, string? field = null)
    {
        Code = code;
        Message = message;
        Field = field;
    }

    public static ApiError ValidationError(string field, string message)
        => new("VALIDATION_ERROR", message, field);

    public static ApiError BusinessRuleViolation(string code, string message)
        => new(code, message);
}