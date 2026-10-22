using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Domain.Entities.Workspace;

[Unique(nameof(Code))]
public class UnitType : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

[Unique(nameof(Code), Within = nameof(IdentityCompanyId))]
public class Unit : BaseEntity, ICompanyScoped
{
    [Parent(typeof(Unit), InUseText = "still has child units")]
    public long? ParentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Parent(typeof(UnitType), InUseText = "is still used by units")]
    public long UnitTypeId { get; set; }

    [Parent(typeof(User), BlocksDelete = false, CheckOnRestore = false)]
    public long? ManagerUserId { get; set; }
}

[Unique(nameof(UnitId), Within = nameof(UserId))]
public class UserUnit : BaseEntity, ICompanyScoped
{
    [Parent(typeof(User), InUseText = "still belongs to units")]
    public long UserId { get; set; }

    [Parent(typeof(Unit), InUseText = "still has members")]
    public long UnitId { get; set; }

    public bool IsPrimary { get; set; }
}

[Unique(nameof(Key), Within = nameof(IdentityTenantId))]
public class TenantSetting : BaseEntity, ITenantScoped
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}

[Unique(nameof(Key), Within = nameof(IdentityCompanyId))]
public class CompanySetting : BaseEntity, ICompanyScoped
{
    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}

[Unique(nameof(Key), Within = nameof(UserId))]
public class UserSetting : BaseEntityRoot
{
    public long UserId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string? Value { get; set; }
}

[Unique(nameof(Code))]
public class ComponentType : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

[Unique(nameof(Code))]
public class DashboardWidget : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Parent(typeof(ComponentType), BlocksDelete = true, InUseText = "is still used by widgets")]
    public long? ComponentTypeId { get; set; }

    [Parent(typeof(Permission), BlocksDelete = false, CheckOnRestore = false)]
    public long? PermissionId { get; set; }

    public string? ModuleCode { get; set; }

    public int DefaultWidth { get; set; } = 4;

    public int DefaultHeight { get; set; } = 3;
}

public enum DashboardScope
{
    Application = 0,
    CompanyType = 1,
    Company = 2,
    User = 3,
    Tenant = 4,

    Role = 5,

    UserType = 6,
}

[Unique(nameof(ScopeId), Within = $"{nameof(Scope)},{nameof(IdentityTenantId)},{nameof(ApplicationTypeId)},{nameof(CompanyId)}")]
public class DashboardLayout : BaseEntity, ITenantScoped
{
    public string Name { get; set; } = string.Empty;

    public DashboardScope Scope { get; set; }

    public long ScopeId { get; set; }

    public long? ApplicationTypeId { get; set; }

    public long? CompanyId { get; set; }

    public string LayoutJson { get; set; } = "[]";
}
