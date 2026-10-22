using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Geography;

[Unique(nameof(Code))]
public class City : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    [Parent(typeof(State))]
    public long StateId { get; set; }
    public string? Description { get; set; }
}