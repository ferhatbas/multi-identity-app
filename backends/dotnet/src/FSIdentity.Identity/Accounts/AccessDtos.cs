using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;

namespace FSIdentity.Identity.Accounts;

public class PermissionRead : AuditedRead
{
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Code { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
    [Sortable]
    public bool IsManagement { get; init; }
    public FSIdentity.Domain.Entities.Accounts.UserType? ManagementRank { get; init; }
}

public class PermissionCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [Required, StringLength(128)]
    [Upper]
    public string Code { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }

    public bool IsManagement { get; init; }

    public FSIdentity.Domain.Entities.Accounts.UserType? ManagementRank { get; init; }
}

public class PermissionUpdate : PermissionCreate
{
    public bool? IsActive { get; init; }
}

public class RoleRead : AuditedRead
{
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string? Code { get; init; }
    [Searchable]
    public string? Description { get; init; }
    [Sortable]
    public long? ApplicationTypeId { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class RoleCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [StringLength(32)]
    [Upper]
    public string? Code { get; init; }

    [StringLength(512)]
    public string? Description { get; init; }

    [Range(1, long.MaxValue)]
    public long? ApplicationTypeId { get; init; }
}

public class RoleUpdate : RoleCreate
{
    public bool? IsActive { get; init; }
}

public class RolePermissionRead : AuditedRead
{
    [Sortable]
    public long RoleId { get; init; }
    [Sortable]
    public long PermissionId { get; init; }
}

public class RolePermissionCreate
{
    [Range(1, long.MaxValue)]
    public long RoleId { get; init; }

    [Range(1, long.MaxValue)]
    public long PermissionId { get; init; }
}

public class RolePermissionUpdate : RolePermissionCreate
{
    public bool? IsActive { get; init; }
}

public class UserCompanyRead : AuditedRead
{
    [Sortable]
    public long UserId { get; init; }
    [Sortable]
    public long CompanyId { get; init; }
    [Sortable]
    public FSIdentity.Domain.Entities.Accounts.UserType MembershipType { get; init; }
}

public class UserCompanyCreate
{
    [Range(1, long.MaxValue)]
    public long UserId { get; init; }

    [Range(1, long.MaxValue)]
    public long CompanyId { get; init; }

    public FSIdentity.Domain.Entities.Accounts.UserType MembershipType { get; init; } =
        FSIdentity.Domain.Entities.Accounts.UserType.Standard;
}

public class UserCompanyUpdate : UserCompanyCreate
{
    public bool? IsActive { get; init; }
}

public class UserCompanyRoleRead : AuditedRead
{
    [Sortable]
    public long UserId { get; init; }
    [Sortable]
    public long CompanyId { get; init; }
    [Sortable]
    public long RoleId { get; init; }
}

public class UserCompanyRoleCreate
{
    [Range(1, long.MaxValue)]
    public long UserId { get; init; }

    [Range(1, long.MaxValue)]
    public long CompanyId { get; init; }

    [Range(1, long.MaxValue)]
    public long RoleId { get; init; }
}

public class UserCompanyRoleUpdate : UserCompanyRoleCreate
{
    public bool? IsActive { get; init; }
}

public class UserCompanyPermissionRead : AuditedRead
{
    [Sortable]
    public long UserId { get; init; }
    [Sortable]
    public long CompanyId { get; init; }
    [Sortable]
    public long PermissionId { get; init; }
    [Sortable]
    public bool IsGranted { get; init; }
}

public class UserCompanyPermissionCreate
{
    [Range(1, long.MaxValue)]
    public long UserId { get; init; }

    [Range(1, long.MaxValue)]
    public long CompanyId { get; init; }

    [Range(1, long.MaxValue)]
    public long PermissionId { get; init; }

    public bool IsGranted { get; init; } = true;
}

public class UserCompanyPermissionUpdate : UserCompanyPermissionCreate
{
    public bool? IsActive { get; init; }
}
