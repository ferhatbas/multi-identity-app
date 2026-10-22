using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Geography;

public class CityRead
{
    public long Id { get; init; }
    [Sortable]
    [Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable]
    public long StateId { get; init; }
    [Searchable]
    public string? Description { get; init; }
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

public class CityCreate
{
    [Required, StringLength(10)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long StateId { get; init; }

    [StringLength(4000)]
    public string? Description { get; init; }
}

public class CityUpdate : CityCreate
{
    public bool? IsActive { get; init; }
}