using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Localization;

public class LanguageRead
{
    public long Id { get; init; }
    [Sortable]
    [Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string Name { get; init; } = string.Empty;
    [Searchable]
    public string? NativeName { get; init; }
    public bool IsRtl { get; init; }
    [Sortable]
    public int DisplayOrder { get; init; }
    public bool IsFallback { get; init; }
    public long? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public long? LastModifiedBy { get; init; }
    public DateTime? LastModifiedAt { get; init; }
    public long? DeletedBy { get; init; }
    public DateTime? DeletedAt { get; init; }
    public bool IsDeleted { get; init; }
    [Sortable]
    public bool IsActive { get; init; }
}

public class LanguageCreate
{
    [Required, StringLength(8)]
    [Lower]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(64)]
    public string Name { get; init; } = string.Empty;

    [StringLength(64)]
    public string? NativeName { get; init; }

    public bool IsRtl { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsFallback { get; init; }
}

public class LanguageUpdate : LanguageCreate
{
    public bool? IsActive { get; init; }
}