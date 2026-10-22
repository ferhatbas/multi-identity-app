using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Entities.Accounts;

namespace FSIdentity.Identity.Auth;

public class LoginRequest
{
    [Required, StringLength(256)]
    public string UserName { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Password { get; init; } = string.Empty;
}

public class RefreshRequest
{
    [Required, StringLength(256)]
    public string RefreshToken { get; init; } = string.Empty;
}

public class ChangeCompanyRequest
{
    [Range(1, long.MaxValue)]
    public long CompanyId { get; init; }
}

public class ChangeApplicationRequest
{
    [Range(1, long.MaxValue)]
    public long ApplicationTypeId { get; init; }
}

public class ChangePasswordRequest
{
    [Required, StringLength(256)]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string NewPassword { get; init; } = string.Empty;
}

public record CompanyMembership(long CompanyId, string Name, string Code, UserType MembershipType);

public class UserSummary
{
    public long Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? LanguageCode { get; init; }

    public string EffectiveLanguageCode { get; init; } = "en";

    public bool IsPlatformAdmin { get; init; }
    public long? TenantId { get; init; }
    public long? ActiveCompanyId { get; init; }
    public long? ActiveApplicationTypeId { get; init; }
    public bool TwoFactorEnabled { get; init; }
    public bool MustChangePassword { get; init; }
    public bool MfaSetupRequired { get; init; }
    public bool EmailConfirmationRequired { get; init; }
    public IReadOnlyList<CompanyMembership> Companies { get; init; } = [];
}

public class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string? RefreshToken { get; init; }
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAt { get; init; }
    public UserSummary User { get; init; } = new();

    public bool TwoFactorRequired { get; init; }

    public string? TwoFactorToken { get; init; }
}

public class MeResponse
{
    public UserSummary User { get; init; } = new();
    public IReadOnlyList<string> Modules { get; init; } = [];

    public IReadOnlyList<string> Permissions { get; init; } = [];
}

public class SetLanguageRequest
{
    [StringLength(8)]
    public string? LanguageCode { get; init; }
}

public class MyLanguage
{
    public string? LanguageCode { get; init; }
    public string EffectiveLanguageCode { get; init; } = "en";
}
