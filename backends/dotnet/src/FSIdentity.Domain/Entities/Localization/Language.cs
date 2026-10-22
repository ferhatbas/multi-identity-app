using FSIdentity.Domain.Crud;

namespace FSIdentity.Domain.Entities.Localization;

[Unique(nameof(Code))]
public class Language : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NativeName { get; set; }

    public bool IsRtl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsFallback { get; set; }
}