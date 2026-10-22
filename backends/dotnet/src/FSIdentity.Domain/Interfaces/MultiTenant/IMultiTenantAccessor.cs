namespace FSIdentity.Domain.Interfaces.MultiTenant;

public interface IMultiTenantAccessor
{
    long? UserId { get; }
    long? TenantId { get; }
    long? CompanyId { get; }

    bool Bypass { get; set; }
    bool BypassCompany { get; set; }
    bool BypassTenant { get; set; }
    bool BypassSoftDelete { get; set; }
}