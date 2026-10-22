using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Workspace;

namespace FSIdentity.Identity.Workspace;

public class UnitTypeRead : AuditedRead
{
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
}

public class UnitTypeCreate
{
    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Name { get; init; } = string.Empty;
}

public class UnitTypeUpdate : UnitTypeCreate
{
    public bool? IsActive { get; init; }
}

public class UnitRead : AuditedRead
{
    public long? ParentId { get; init; }
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
    [Sortable]
    public long UnitTypeId { get; init; }
    public long? ManagerUserId { get; init; }
    [MapsFrom("IdentityCompanyId")]
    public long? CompanyId { get; init; }
}

public class UnitCreate
{
    [Range(1, long.MaxValue)]
    public long? ParentId { get; init; }

    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }

    [Range(1, long.MaxValue)]
    public long UnitTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? ManagerUserId { get; init; }
}

public class UnitUpdate : UnitCreate
{
    public bool? IsActive { get; init; }
}

public class UnitNode
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public long UnitTypeId { get; init; }
    public long? ManagerUserId { get; init; }
    public List<UnitNode> Children { get; init; } = [];
}

public class UserUnitRead : AuditedRead
{
    [Sortable]
    public long UserId { get; init; }
    [Sortable]
    public long UnitId { get; init; }
    [Sortable]
    public bool IsPrimary { get; init; }
}

public class UserUnitCreate
{
    [Range(1, long.MaxValue)]
    public long UserId { get; init; }

    [Range(1, long.MaxValue)]
    public long UnitId { get; init; }

    public bool IsPrimary { get; init; }
}

public class UserUnitUpdate : UserUnitCreate
{
    public bool? IsActive { get; init; }
}

public class TenantSettingRead : AuditedRead
{
    [Sortable, Searchable]
    public string Key { get; init; } = string.Empty;
    [Searchable]
    public string? Value { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class TenantSettingCreate
{
    [Required, StringLength(64), RegularExpression(SettingKeys.Pattern)]
    public string Key { get; init; } = string.Empty;

    [StringLength(4000)]
    public string? Value { get; init; }
}

public class TenantSettingUpdate : TenantSettingCreate
{
    public bool? IsActive { get; init; }
}

public class CompanySettingRead : AuditedRead
{
    [Sortable, Searchable]
    public string Key { get; init; } = string.Empty;
    [Searchable]
    public string? Value { get; init; }
    [MapsFrom("IdentityCompanyId")]
    public long? CompanyId { get; init; }
}

public class CompanySettingCreate
{
    [Required, StringLength(64), RegularExpression(SettingKeys.Pattern)]
    public string Key { get; init; } = string.Empty;

    [StringLength(4000)]
    public string? Value { get; init; }
}

public class CompanySettingUpdate : CompanySettingCreate
{
    public bool? IsActive { get; init; }
}

public static class SettingKeys
{
    public const string Pattern = "^[A-Za-z][A-Za-z0-9_.-]{0,63}$";
}

public class SettingValue
{
    public string Key { get; init; } = string.Empty;
    public string? Value { get; init; }

    public string Source { get; init; } = string.Empty;
}

public class UserSettingWrite
{
    [StringLength(4000)]
    public string? Value { get; init; }
}

public class ComponentTypeRead : AuditedRead
{
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
}

public class ComponentTypeCreate
{
    [Required, StringLength(32)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(128)]
    public string Name { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }
}

public class ComponentTypeUpdate : ComponentTypeCreate
{
    public bool? IsActive { get; init; }
}

public class DashboardWidgetRead : AuditedRead
{
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Title { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
    public long? ComponentTypeId { get; init; }
    public long? PermissionId { get; init; }
    public string? ModuleCode { get; init; }
    public int DefaultWidth { get; init; }
    public int DefaultHeight { get; init; }
}

public class DashboardWidgetCreate
{
    [Required, StringLength(48)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string Title { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }

    [Range(1, long.MaxValue)]
    public long? ComponentTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? PermissionId { get; init; }

    [StringLength(48)]
    [Upper]
    public string? ModuleCode { get; init; }

    [Range(1, DashboardLayoutCodec.Columns)]
    public int DefaultWidth { get; init; } = 4;

    [Range(1, 24)]
    public int DefaultHeight { get; init; } = 3;
}

public class DashboardWidgetUpdate : DashboardWidgetCreate
{
    public bool? IsActive { get; init; }
}

public class DashboardLayoutRead : AuditedRead
{
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable]
    public DashboardScope Scope { get; init; }
    [Sortable]
    public long ScopeId { get; init; }
    public long? ApplicationTypeId { get; init; }
    public long? CompanyId { get; init; }
    public string LayoutJson { get; init; } = "[]";
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class DashboardLayoutCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    public DashboardScope Scope { get; init; }

    [Range(0, long.MaxValue)]
    public long ScopeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? ApplicationTypeId { get; init; }

    [Range(1, long.MaxValue)]
    public long? CompanyId { get; init; }

    [Required]
    public string LayoutJson { get; init; } = "[]";
}

public class DashboardLayoutUpdate : DashboardLayoutCreate
{
    public bool? IsActive { get; init; }
}

public class DashboardItem
{
    public string Widget { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? ComponentType { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int W { get; init; }
    public int H { get; init; }
}

public class DashboardView
{
    public string Source { get; init; } = "default";

    public DashboardScope? SourceScope { get; init; }
    public long? SourceScopeId { get; init; }
    public long? SourceCompanyId { get; init; }

    public int HiddenCount { get; init; }
    public long? LayoutId { get; init; }
    public List<DashboardItem> Items { get; init; } = [];
}

public class DashboardLayoutWrite
{
    [Required]
    public List<DashboardPlacement> Items { get; init; } = [];
}

public class DashboardPlacement
{
    [Required, StringLength(48)]
    public string Widget { get; init; } = string.Empty;
    public int X { get; init; }
    public int Y { get; init; }
    public int W { get; init; }
    public int H { get; init; }
}
