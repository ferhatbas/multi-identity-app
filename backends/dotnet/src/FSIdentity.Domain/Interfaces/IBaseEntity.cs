namespace FSIdentity.Domain.Interfaces;
public interface IBaseEntity :IBaseEntityRoot
{
    long? IdentityCompanyId { get; set; }
    long? IdentityTenantId { get; set; }
}