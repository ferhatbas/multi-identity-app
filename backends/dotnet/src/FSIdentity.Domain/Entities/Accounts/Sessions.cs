using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Accounts;

[NoAudit]
public class LoginAttempt : BaseEntityRoot
{
    public string UserName { get; set; } = string.Empty;

    public long? UserId { get; set; }

    public bool Succeeded { get; set; }

    public string? Reason { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}

public class UserLockout : BaseEntityRoot
{
    [Parent(typeof(User), BlocksDelete = false, CheckOnRestore = false)]
    public long UserId { get; set; }

    public DateTime LockedAt { get; set; }

    public DateTime LockedUntil { get; set; }

    public string? Reason { get; set; }
}

[NoAudit]
public class UserSession : BaseEntityRoot
{
    public long UserId { get; set; }

    public long? CompanyId { get; set; }

    public long? ApplicationTypeId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime LastSeenAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public long? ImpersonatorUserId { get; set; }
}

[NoAudit]
public class RefreshToken : BaseEntityRoot
{
    public long UserId { get; set; }

    public long SessionId { get; set; }

    [AuditIgnore]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? UsedAt { get; set; }
}
