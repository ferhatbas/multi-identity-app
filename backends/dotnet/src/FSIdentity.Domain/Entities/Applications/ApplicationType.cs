using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Applications;

[Unique(nameof(Code))]
public class ApplicationType : BaseEntityRoot
{
    public const string ManagementCode = "MANAGEMENT";

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }
}