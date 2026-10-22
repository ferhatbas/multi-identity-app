namespace FSIdentity.Domain.Audit;

public interface IAuditContext
{
    string? UserName { get; }
    long? ImpersonatorId { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }
}
