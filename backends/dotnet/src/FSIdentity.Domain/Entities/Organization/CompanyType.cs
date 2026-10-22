using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Organization;

[Unique(nameof(Code))]
public class CompanyType : BaseEntityRoot
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }
}
