using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Organization;

[Unique(nameof(Code))]
public class Tenant : BaseEntityRoot
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? LanguageCode { get; set; }
}
