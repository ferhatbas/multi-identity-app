using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Geography;

using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Domain.Entities.Organization;

[Unique(nameof(Code), Within = nameof(IdentityTenantId))]
[Parent(typeof(Tenant), Column = nameof(IdentityTenantId), CheckOnWrite = false, ChildrenAcrossScopes = true)]
public class Company : BaseEntity, ITenantScoped
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }
    [Parent(typeof(Country), BlocksDelete = false, CheckOnRestore = false)]
    public long? CountryId { get; set; }

    public string? LanguageCode { get; set; }

    [Parent(typeof(CompanyType), CheckOnRestore = false, ChildrenAcrossScopes = true, InUseText = "is still used by companies")]
    public long? CompanyTypeId { get; set; }
}
