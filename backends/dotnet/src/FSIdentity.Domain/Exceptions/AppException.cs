namespace FSIdentity.Domain.Exceptions;

public class AppException : Exception
{
    public int StatusCode { get; }
    public string ErrorCode { get; }

    public AppException(int statusCode, string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base(400, "BAD_REQUEST", message)
    {
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(404, "NOT_FOUND", message)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(409, "CONFLICT", message)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message) : base(403, "FORBIDDEN", message)
    {
    }
}

public class ModuleNotGrantedException : AppException
{
    public ModuleNotGrantedException(string message) : base(403, "MODULE_NOT_GRANTED", message)
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException(string message = "Authentication is required") : base(401, "UNAUTHORIZED", message)
    {
    }
}

public class AccountLockedException : AppException
{
    public AccountLockedException(string message) : base(423, "ACCOUNT_LOCKED", message)
    {
    }
}

public class ReaderReadOnlyException : AppException
{
    public ReaderReadOnlyException(string message) : base(403, "READ_ONLY", message)
    {
    }
}

public class PermissionDeniedException : AppException
{
    public PermissionDeniedException(string message) : base(403, "PERMISSION_DENIED", message)
    {
    }
}

public class PasswordChangeRequiredException : AppException
{
    public PasswordChangeRequiredException(string message) : base(403, "PASSWORD_CHANGE_REQUIRED", message)
    {
    }
}

public class SodConflictException : AppException
{
    public SodConflictException(string message) : base(409, "SOD_CONFLICT", message)
    {
    }
}

public class MfaSetupRequiredException : AppException
{
    public MfaSetupRequiredException(string message) : base(403, "MFA_SETUP_REQUIRED", message)
    {
    }
}

public class EmailConfirmationRequiredException : AppException
{
    public EmailConfirmationRequiredException(string message) : base(403, "EMAIL_CONFIRMATION_REQUIRED", message)
    {
    }
}
