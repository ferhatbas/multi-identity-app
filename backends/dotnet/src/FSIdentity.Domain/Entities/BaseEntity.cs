using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Entities;

public abstract class BaseEntity : BaseEntityRoot, IBaseEntity
{
    public long? IdentityCompanyId { get; set; }
    public long? IdentityTenantId { get; set; }
}