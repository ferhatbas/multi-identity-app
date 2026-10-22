using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Identity.Accounts;
using FSIdentity.Identity.Authorization;

namespace FSIdentity.Identity.Users;

public class UserRead : AuditedRead
{
    [Sortable, Searchable]
    public string UserName { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string Email { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string FirstName { get; init; } = string.Empty;
    [Sortable, Searchable]
    public string LastName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public bool EmailConfirmed { get; init; }
    public bool ShouldChangePasswordOnNextLogin { get; init; }
    public long? ActiveCompanyId { get; init; }
    public string? LanguageCode { get; init; }
    public bool IsPlatformAdmin { get; init; }
    public bool TwoFactorEnabled { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class UserProfile
{
    [Required, StringLength(256)]
    [Lower]
    public string UserName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Lower]
    public string Email { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string FirstName { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string LastName { get; init; } = string.Empty;

    [StringLength(32)]
    public string? PhoneNumber { get; init; }

    [StringLength(8)]
    [Lower]
    public string? LanguageCode { get; init; }
}

public class UserCreate : UserProfile
{
    [Required, StringLength(256)]
    [MapIgnore]
    public string Password { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    [MapIgnore]
    public long? TenantId { get; init; }

    public bool ShouldChangePasswordOnNextLogin { get; init; } = true;
}

public class UserUpdate : UserProfile
{
    public bool? IsActive { get; init; }

    [Range(1, long.MaxValue)]
    public long? ActiveCompanyId { get; init; }

    public bool? ShouldChangePasswordOnNextLogin { get; init; }
}

public class EffectivePermissionsRead
{
    public long UserId { get; init; }
    public long? CompanyId { get; init; }
    public long? ApplicationTypeId { get; init; }
    public IReadOnlyList<EffectivePermission> Granted { get; init; } = [];
    public IReadOnlyList<string> Denied { get; init; } = [];
}

public class UserCopyRequest
{
    [Required, StringLength(256)]
    [Lower]
    public string UserName { get; init; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    [Lower]
    public string Email { get; init; } = string.Empty;

    [StringLength(256)]
    public string? FirstName { get; init; }

    [StringLength(256)]
    public string? LastName { get; init; }

    [StringLength(256)]
    public string? Password { get; init; }

    public bool CopyMemberships { get; init; } = true;

    public bool CopyRoles { get; init; } = true;

    public bool CopyPermissions { get; init; } = true;
}

public class RoleCopyRequest
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [StringLength(32)]
    public string? Code { get; init; }
}

public class BulkUserRow
{
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    public string? Password { get; init; }

    public long? CompanyId { get; init; }

    public UserType MembershipType { get; init; } = UserType.Standard;

    public List<long> RoleIds { get; init; } = [];
}

public class BulkUserImportRequest
{
    public const int MaxRows = 500;

    [Required, MinLength(1), MaxLength(MaxRows)]
    public List<BulkUserRow> Users { get; init; } = [];

    [Range(1, long.MaxValue)]
    public long? TenantId { get; init; }

    public bool Atomic { get; init; }
}

public record ImportedUser(long Id, string UserName, string? TemporaryPassword);

public record ImportFailure(int Row, string UserName, string Error);

public record BulkUserImportResult(int Created, bool RolledBack, IReadOnlyList<ImportedUser> Users, IReadOnlyList<ImportFailure> Failed);

public record CopiedUser(UserRead User, string? TemporaryPassword);
