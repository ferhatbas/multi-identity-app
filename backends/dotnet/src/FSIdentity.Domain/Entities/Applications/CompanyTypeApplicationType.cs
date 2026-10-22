using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Organization;

namespace FSIdentity.Domain.Entities.Applications;

[Unique(nameof(ApplicationTypeId), Within = nameof(CompanyTypeId))]
public class CompanyTypeApplicationType : BaseEntityRoot
{
    [Parent(typeof(CompanyType), InUseText = "still has application mappings")]
    public long CompanyTypeId { get; set; }

    public CompanyType? CompanyType { get; set; }

    [Parent(typeof(ApplicationType), InUseText = "is still mapped to company types")]
    public long ApplicationTypeId { get; set; }

    public ApplicationType? ApplicationType { get; set; }

    public bool IsDefault { get; set; }
}