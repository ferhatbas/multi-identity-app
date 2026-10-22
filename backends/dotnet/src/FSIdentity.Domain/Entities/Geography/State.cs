using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Geography;

[Unique(nameof(Code))]
public class State : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [Parent(typeof(Country))]
    public long CountryId { get; set; }
    public string? Description { get; set; }
}