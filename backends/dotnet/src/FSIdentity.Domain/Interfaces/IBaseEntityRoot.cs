namespace FSIdentity.Domain.Interfaces;

public interface IBaseEntityRoot : IAuditable, ISoftDelete
{
    long Id { get; set; }
    bool IsActive { get; set; }
}