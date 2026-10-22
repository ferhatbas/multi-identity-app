using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Organization;

public class CompanyTypeRead
{
    public long Id { get; init; }
    [Sortable]
    [Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string Code { get; init; } = string.Empty;
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

public class CompanyTypeCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(16)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }
}

public class CompanyTypeUpdate : CompanyTypeCreate
{
    public bool? IsActive { get; init; }
}
