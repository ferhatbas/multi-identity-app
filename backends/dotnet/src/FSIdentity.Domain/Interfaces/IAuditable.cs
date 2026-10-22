namespace FSIdentity.Domain.Interfaces;

public interface IAuditable
{
    long? CreatedBy { get; set; }
    DateTime? CreatedAt { get; set; }
    long? LastModifiedBy { get; set; }
    DateTime? LastModifiedAt { get; set; }
    long? DeletedBy { get; set; }
    DateTime? DeletedAt { get; set; }
}