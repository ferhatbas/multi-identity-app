namespace FSIdentity.Domain.Audit;

public class AuditLog
{
    public long Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string Action { get; set; } = string.Empty;

    public long? UserId { get; set; }
    public string? UserName { get; set; }

    public long? ImpersonatedBy { get; set; }

    public long? AuditTenantId { get; set; }
    public long? AuditCompanyId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public string? CorrelationId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? ChangedFields { get; set; }
}

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class NoAuditAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditIgnoreAttribute : Attribute;
