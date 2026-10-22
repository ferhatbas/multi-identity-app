using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Geography;

public class CountryRead
{
    public long Id { get; init; }
    [Sortable]
    [Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string? NameGlobal { get; init; }
    [Sortable]
    [Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string? Region { get; init; }
    public string FormatCode { get; init; } = string.Empty;
    public string? LanguageCode { get; init; }
    public string? DateFormat { get; init; }
    public string? DateTimeFormat { get; init; }
    [Sortable]
    public string? CurrencyCode { get; init; }
    public string? CurrencySymbol { get; init; }
    public string? PriceFormat { get; init; }
    public string? TimeZone { get; init; }
    public long? CreatedBy { get; init; }
    [Sortable]
    public DateTime? CreatedAt { get; init; }
    public long? LastModifiedBy { get; init; }
    public DateTime? LastModifiedAt { get; init; }
    public long? DeletedBy { get; init; }
    public DateTime? DeletedAt { get; init; }
    public bool IsDeleted { get; init; }
    [Sortable]
    public bool IsActive { get; init; }
}

public class CountryCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [StringLength(256)]
    public string? NameGlobal { get; init; }

    [Required, StringLength(16)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [StringLength(256)]
    public string? Region { get; init; }

    [Required, StringLength(8)]
    public string FormatCode { get; init; } = string.Empty;

    [StringLength(8)]
    public string? LanguageCode { get; init; }

    [StringLength(16)]
    public string? DateFormat { get; init; }

    [StringLength(50)]
    public string? DateTimeFormat { get; init; }

    [StringLength(8)]
    public string? CurrencyCode { get; init; }

    [StringLength(8)]
    public string? CurrencySymbol { get; init; }

    [StringLength(32)]
    public string? PriceFormat { get; init; }

    [StringLength(64)]
    public string? TimeZone { get; init; }
}

public class CountryUpdate : CountryCreate
{
    public bool? IsActive { get; init; }
}