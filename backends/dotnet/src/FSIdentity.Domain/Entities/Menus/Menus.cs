using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Domain.Entities.Menus;

[Unique(nameof(Code))]
public class MenuType : BaseEntityRoot
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

[Unique(nameof(Code))]
public class Menu : BaseEntityRoot
{
    [Parent(typeof(Menu), BlocksDelete = true, InUseText = "still has child entries")]
    public long? ParentId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    [Parent(typeof(MenuType), BlocksDelete = true, InUseText = "still has entries")]
    public long? MenuTypeId { get; set; }

    [Parent(typeof(ApplicationType), BlocksDelete = false, CheckOnRestore = false)]
    public long? ApplicationTypeId { get; set; }

    [Parent(typeof(Permission), BlocksDelete = false, CheckOnRestore = false)]
    public long? PermissionId { get; set; }

    public string? ModuleCode { get; set; }

    public string? FeatureCode { get; set; }
}

[Unique(nameof(LanguageCode), Within = nameof(MenuId))]
public class MenuLocalization : BaseEntityRoot
{
    [Parent(typeof(Menu), InUseText = "still has translations")]
    public long MenuId { get; set; }

    public string LanguageCode { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}

[Unique(nameof(CompanyTypeId), Within = nameof(MenuId))]
public class MenuCompanyType : BaseEntityRoot
{
    [Parent(typeof(Menu), InUseText = "is limited to company types")]
    public long MenuId { get; set; }

    [Parent(typeof(CompanyType), InUseText = "has menus limited to it")]
    public long CompanyTypeId { get; set; }
}

[Unique(nameof(MenuId), Within = nameof(IdentityTenantId))]
public class MenuPermissionOverride : BaseEntity, ITenantScoped
{
    [Parent(typeof(Menu), InUseText = "is overridden by tenants")]
    public long MenuId { get; set; }

    [Parent(typeof(Permission), BlocksDelete = false, CheckOnRestore = false)]
    public long? PermissionId { get; set; }
}

[Unique(nameof(FeatureCode), Within = nameof(IdentityTenantId))]
public class MenuFeatureToggle : BaseEntity, ITenantScoped
{
    public string FeatureCode { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}

[Unique(nameof(MenuId), Within = nameof(ApplicationTypeId))]
public class MenuApplicationTypeLayout : BaseEntityRoot
{
    [Parent(typeof(ApplicationType), InUseText = "has menu layouts")]
    public long ApplicationTypeId { get; set; }

    [Parent(typeof(Menu), InUseText = "has layouts")]
    public long MenuId { get; set; }

    public bool IsHidden { get; set; }

    public int? DisplayOrder { get; set; }
}

[Unique(nameof(MenuId), Within = nameof(CompanyTypeId))]
public class MenuCompanyTypeLayout : BaseEntityRoot
{
    [Parent(typeof(CompanyType), InUseText = "has menu layouts")]
    public long CompanyTypeId { get; set; }

    [Parent(typeof(Menu), InUseText = "has layouts")]
    public long MenuId { get; set; }

    public bool IsHidden { get; set; }

    public int? DisplayOrder { get; set; }
}
