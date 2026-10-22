using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Organization;

public class TenantRead
{
    public long Id { get; init; }
    [Sortable]
    [Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable]
    [Searchable]
    public string Code { get; init; } = string.Empty;
    public string? LanguageCode { get; init; }
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

public class TenantCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [StringLength(8)]
    [Lower]
    public string? LanguageCode { get; init; }
}

public class TenantUpdate : TenantCreate
{
    public bool? IsActive { get; init; }
}
