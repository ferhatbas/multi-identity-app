using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Domain.Entities.Accounts;

public enum UserType
{
    Reader = 0,
    Standard = 1,
    CompanyAdmin = 2,
    TenantAdmin = 3,
    PlatformAdmin = 4,
}

[Unique(nameof(UserName), AcrossScopes = true)]
[Unique(nameof(Email), AcrossScopes = true)]
public class User : BaseEntity, ITenantScoped
{
    public string UserName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    [AuditIgnore]
    public string PasswordHash { get; set; } = string.Empty;

    public bool EmailConfirmed { get; set; }

    public bool ShouldChangePasswordOnNextLogin { get; set; }

    public DateTime? LastPasswordChangeAt { get; set; }

    public long? ActiveCompanyId { get; set; }

    public long? ActiveApplicationTypeId { get; set; }

    public bool IsPlatformAdmin { get; set; }

    public string? LanguageCode { get; set; }

    public bool TwoFactorEnabled { get; set; }

    [AuditIgnore]
    public string? TwoFactorSecret { get; set; }

    public DateTime? TwoFactorActivatedAt { get; set; }

    [AuditIgnore]
    public long? TwoFactorLastStep { get; set; }
}
