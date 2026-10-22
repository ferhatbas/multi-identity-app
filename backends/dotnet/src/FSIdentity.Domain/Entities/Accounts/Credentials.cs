using FSIdentity.Domain.Audit;

namespace FSIdentity.Domain.Entities.Accounts;

[NoAudit]
public class PasswordReset : BaseEntityRoot
{
    public long UserId { get; set; }

    [AuditIgnore]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }

    public string? IpAddress { get; set; }
}

[NoAudit]
public class EmailConfirmation : BaseEntityRoot
{
    public long UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    [AuditIgnore]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }
}

[NoAudit]
public class UserRecoveryCode : BaseEntityRoot
{
    public long UserId { get; set; }

    [AuditIgnore]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime? UsedAt { get; set; }
}
