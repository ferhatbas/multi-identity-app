using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Applications;

public class ApplicationTypeRead
{
    public long Id { get; init; }
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
    public string? Icon { get; init; }
    [Sortable]
    public int DisplayOrder { get; init; }
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

public class ApplicationTypeCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }

    [StringLength(64)]
    public string? Icon { get; init; }

    public int DisplayOrder { get; init; }
}

public class ApplicationTypeUpdate : ApplicationTypeCreate
{
    public bool? IsActive { get; init; }
}

public class AppModuleRead
{
    public long Id { get; init; }
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
    [Sortable]
    public int DisplayOrder { get; init; }
    [Sortable]
    public bool IsCore { get; init; }
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

public class AppModuleCreate
{
    [Required, StringLength(48)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Name { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }

    public int DisplayOrder { get; init; }

    public bool IsCore { get; init; }
}

public class AppModuleUpdate : AppModuleCreate
{
    public bool? IsActive { get; init; }
}

public class CompanyTypeApplicationTypeRead
{
    public long Id { get; init; }
    [Sortable]
    public long CompanyTypeId { get; init; }
    [Sortable]
    public long ApplicationTypeId { get; init; }
    [Sortable]
    public bool IsDefault { get; init; }
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

public class CompanyTypeApplicationTypeCreate
{
    [Range(1, long.MaxValue)]
    public long CompanyTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long ApplicationTypeId { get; init; }

    public bool IsDefault { get; init; }
}

public class CompanyTypeApplicationTypeUpdate : CompanyTypeApplicationTypeCreate
{
    public bool? IsActive { get; init; }
}

public class ApplicationTypeModuleRead
{
    public long Id { get; init; }
    [Sortable]
    public long ApplicationTypeId { get; init; }
    [Sortable]
    public long AppModuleId { get; init; }
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

public class ApplicationTypeModuleCreate
{
    [Range(1, long.MaxValue)]
    public long ApplicationTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long AppModuleId { get; init; }
}

public class ApplicationTypeModuleUpdate : ApplicationTypeModuleCreate
{
    public bool? IsActive { get; init; }
}