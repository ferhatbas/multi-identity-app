using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Applications;
using FSIdentity.Domain.Entities.Organization;
using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Domain.Entities.Accounts;

[Unique(nameof(Name), Within = $"{nameof(IdentityTenantId)},{nameof(ApplicationTypeId)}")]
public class Role : BaseEntity, ITenantScoped
{
    public string Name { get; set; } = string.Empty;

    public string? Code { get; set; }

    public string? Description { get; set; }

    [Parent(typeof(ApplicationType), BlocksDelete = false, CheckOnRestore = false)]
    public long? ApplicationTypeId { get; set; }
}

[Unique(nameof(Code))]
public class Permission : BaseEntityRoot
{
    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsManagement { get; set; }

    public UserType? ManagementRank { get; set; }
}

[Unique(nameof(PermissionId), Within = nameof(RoleId))]
public class RolePermission : BaseEntity, ITenantScoped
{
    [Parent(typeof(Role), InUseText = "still has permissions")]
    public long RoleId { get; set; }

    [Parent(typeof(Permission), InUseText = "is still granted to roles")]
    public long PermissionId { get; set; }
}

[Unique(nameof(CompanyId), Within = nameof(UserId))]
public class UserCompany : BaseEntity, ITenantScoped
{
    [Parent(typeof(User), InUseText = "still has company memberships")]
    public long UserId { get; set; }

    [Parent(typeof(Company), InUseText = "still has members")]
    public long CompanyId { get; set; }

    public UserType MembershipType { get; set; } = UserType.Standard;
}

[Unique(nameof(RoleId), Within = $"{nameof(UserId)},{nameof(CompanyId)}")]
public class UserCompanyRole : BaseEntity, ITenantScoped
{
    [Parent(typeof(User), InUseText = "still has roles")]
    public long UserId { get; set; }

    [Parent(typeof(Company), InUseText = "still has user roles")]
    public long CompanyId { get; set; }

    [Parent(typeof(Role), InUseText = "is still held by users")]
    public long RoleId { get; set; }
}

[Unique(nameof(PermissionId), Within = $"{nameof(UserId)},{nameof(CompanyId)}")]
public class UserCompanyPermission : BaseEntity, ITenantScoped
{
    [Parent(typeof(User), InUseText = "still has permission overrides")]
    public long UserId { get; set; }

    [Parent(typeof(Company), InUseText = "still has permission overrides")]
    public long CompanyId { get; set; }

    [Parent(typeof(Permission), InUseText = "is still overridden for users")]
    public long PermissionId { get; set; }

    public bool IsGranted { get; set; } = true;
}
