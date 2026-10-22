using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Entities;

public abstract class BaseEntityRoot : IBaseEntityRoot
{
    public long Id { get; set; }
    public DateTime? CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }
    public long? LastModifiedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    public long? DeletedBy { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsActive { get; set; } = true;
}