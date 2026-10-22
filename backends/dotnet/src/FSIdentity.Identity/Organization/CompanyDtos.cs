using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Organization;

public class CompanyRead
{
    public long Id { get; init; }
    [Sortable]
    [Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string Code { get; init; } = string.Empty;
    [Searchable]
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Website { get; init; }
    public string? Address { get; init; }
    public string? Address2 { get; init; }
    [Sortable]
    [Searchable]
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
    public long? CountryId { get; init; }
    public string? LanguageCode { get; init; }
    public long? CompanyTypeId { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
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

public class CompanyUpdate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [EmailAddress, StringLength(256)]
    public string? Email { get; init; }

    [StringLength(32)]
    public string? PhoneNumber { get; init; }

    [StringLength(256)]
    public string? Website { get; init; }

    [StringLength(256)]
    public string? Address { get; init; }

    [StringLength(256)]
    public string? Address2 { get; init; }

    [StringLength(256)]
    public string? City { get; init; }

    [StringLength(256)]
    public string? State { get; init; }

    [StringLength(64)]
    public string? Zip { get; init; }

    [Range(1, long.MaxValue)]
    public long? CountryId { get; init; }

    [StringLength(8)]
    [Lower]
    public string? LanguageCode { get; init; }

    [Range(1, long.MaxValue)]
    public long? CompanyTypeId { get; init; }

    public bool? IsActive { get; init; }
}

public class CompanyCreate : CompanyUpdate
{
    [Range(1, long.MaxValue)]
    [MapIgnore]
    public long? TenantId { get; init; }
}
