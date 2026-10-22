using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Menus;

public class MenuTypeRead : AuditedRead
{
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
}

public class MenuTypeCreate
{
    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Name { get; init; } = string.Empty;
}

public class MenuTypeUpdate : MenuTypeCreate
{
    public bool? IsActive { get; init; }
}

public class MenuRead : AuditedRead
{
    public long? ParentId { get; init; }
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Title { get; init; } = string.Empty;
    [Searchable]
    public string? Path { get; init; }
    public string? Icon { get; init; }
    [Sortable]
    public int DisplayOrder { get; init; }
    public long? MenuTypeId { get; init; }
    public long? ApplicationTypeId { get; init; }
    public long? PermissionId { get; init; }
    public string? ModuleCode { get; init; }
    public string? FeatureCode { get; init; }
}

public class MenuCreate
{
    [Range(1, long.MaxValue)]
    public long? ParentId { get; init; }

    [Required, StringLength(64)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Title { get; init; } = string.Empty;

    [StringLength(256)]
    public string? Path { get; init; }

    [StringLength(64)]
    public string? Icon { get; init; }

    public int DisplayOrder { get; init; }

    [Range(1, long.MaxValue)]
    public long? MenuTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? ApplicationTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? PermissionId { get; init; }

    [StringLength(48)]
    [Upper]
    public string? ModuleCode { get; init; }

    [StringLength(64)]
    [Upper]
    public string? FeatureCode { get; init; }
}

public class MenuUpdate : MenuCreate
{
    public bool? IsActive { get; init; }
}

public class MenuLocalizationRead : AuditedRead
{
    [Sortable]
    public long MenuId { get; init; }
    [Sortable, Searchable]
    public string LanguageCode { get; init; } = string.Empty;
    [Searchable]
    public string Title { get; init; } = string.Empty;
}

public class MenuLocalizationCreate
{
    [Range(1, long.MaxValue)]
    public long MenuId { get; init; }

    [Required, StringLength(8)]
    [Lower]
    public string LanguageCode { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Title { get; init; } = string.Empty;
}

public class MenuLocalizationUpdate : MenuLocalizationCreate
{
    public bool? IsActive { get; init; }
}

public class MenuCompanyTypeRead : AuditedRead
{
    [Sortable]
    public long MenuId { get; init; }
    [Sortable]
    public long CompanyTypeId { get; init; }
}

public class MenuCompanyTypeCreate
{
    [Range(1, long.MaxValue)]
    public long MenuId { get; init; }

    [Range(1, long.MaxValue)]
    public long CompanyTypeId { get; init; }
}

public class MenuCompanyTypeUpdate : MenuCompanyTypeCreate
{
    public bool? IsActive { get; init; }
}

public class MenuPermissionOverrideRead : AuditedRead
{
    [Sortable]
    public long MenuId { get; init; }
    public long? PermissionId { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class MenuPermissionOverrideCreate
{
    [Range(1, long.MaxValue)]
    public long MenuId { get; init; }

    [Range(1, long.MaxValue)]
    public long? PermissionId { get; init; }
}

public class MenuPermissionOverrideUpdate : MenuPermissionOverrideCreate
{
    public bool? IsActive { get; init; }
}

public class MenuFeatureToggleRead : AuditedRead
{
    [Sortable, Searchable]
    public string FeatureCode { get; init; } = string.Empty;
    [Sortable]
    public bool IsEnabled { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class MenuFeatureToggleCreate
{
    [Required, StringLength(64)]
    [Upper]
    public string FeatureCode { get; init; } = string.Empty;

    public bool IsEnabled { get; init; } = true;
}

public class MenuFeatureToggleUpdate : MenuFeatureToggleCreate
{
    public bool? IsActive { get; init; }
}

public class MenuApplicationTypeLayoutRead : AuditedRead
{
    [Sortable]
    public long ApplicationTypeId { get; init; }
    [Sortable]
    public long MenuId { get; init; }
    public bool IsHidden { get; init; }
    public int? DisplayOrder { get; init; }
}

public class MenuApplicationTypeLayoutCreate
{
    [Range(1, long.MaxValue)]
    public long ApplicationTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long MenuId { get; init; }

    public bool IsHidden { get; init; }

    public int? DisplayOrder { get; init; }
}

public class MenuApplicationTypeLayoutUpdate : MenuApplicationTypeLayoutCreate
{
    public bool? IsActive { get; init; }
}

public class MenuCompanyTypeLayoutRead : AuditedRead
{
    [Sortable]
    public long CompanyTypeId { get; init; }
    [Sortable]
    public long MenuId { get; init; }
    public bool IsHidden { get; init; }
    public int? DisplayOrder { get; init; }
}

public class MenuCompanyTypeLayoutCreate
{
    [Range(1, long.MaxValue)]
    public long CompanyTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long MenuId { get; init; }

    public bool IsHidden { get; init; }

    public int? DisplayOrder { get; init; }
}

public class MenuCompanyTypeLayoutUpdate : MenuCompanyTypeLayoutCreate
{
    public bool? IsActive { get; init; }
}

public class MenuNode
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Path { get; init; }
    public string? Icon { get; init; }
    public int DisplayOrder { get; init; }
    public List<MenuNode> Children { get; init; } = [];
}
