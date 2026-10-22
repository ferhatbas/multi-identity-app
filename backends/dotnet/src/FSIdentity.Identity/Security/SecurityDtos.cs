using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Entities.Accounts;

namespace FSIdentity.Identity.Security;

public class SecurityPolicyRead : AuditedRead
{
    public string? Name { get; init; }
    public int? MinPasswordLength { get; init; }
    public bool? PasswordRequiresLetter { get; init; }
    public bool? PasswordRequiresDigit { get; init; }
    public bool? PasswordRequiresSymbol { get; init; }
    public bool? PasswordRequiresMixedCase { get; init; }
    public int? PasswordMaxAgeDays { get; init; }
    public int? MaxFailedAttempts { get; init; }
    public int? LockoutWindowMinutes { get; init; }
    public int? LockoutMinutes { get; init; }
    public int? SessionDays { get; init; }
    public UserType? RequireMfaFromRank { get; init; }
    public bool? RequireConfirmedEmail { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class SecurityPolicyCreate
{
    [StringLength(256)]
    public string? Name { get; init; }

    [Range(6, 64)]
    public int? MinPasswordLength { get; init; }

    public bool? PasswordRequiresLetter { get; init; }
    public bool? PasswordRequiresDigit { get; init; }
    public bool? PasswordRequiresSymbol { get; init; }
    public bool? PasswordRequiresMixedCase { get; init; }

    [Range(1, 3650)]
    public int? PasswordMaxAgeDays { get; init; }

    [Range(1, 100)]
    public int? MaxFailedAttempts { get; init; }

    [Range(1, 1440)]
    public int? LockoutWindowMinutes { get; init; }

    [Range(1, 1440)]
    public int? LockoutMinutes { get; init; }

    [Range(1, 365)]
    public int? SessionDays { get; init; }

    public UserType? RequireMfaFromRank { get; init; }

    public bool? RequireConfirmedEmail { get; init; }

    [Range(1, long.MaxValue)]
    [MapIgnore]
    public long? TenantId { get; init; }
}

public class SecurityPolicyUpdate : SecurityPolicyCreate
{
    public bool? IsActive { get; init; }
}

public class SoDRuleRead : AuditedRead
{
    [Sortable, Searchable]
    public string Name { get; init; } = string.Empty;
    [Searchable]
    public string? Description { get; init; }
    [Sortable]
    public long PermissionAId { get; init; }
    [Sortable]
    public long PermissionBId { get; init; }
    [MapsFrom("IdentityTenantId")]
    public long? TenantId { get; init; }
}

public class SoDRuleCreate
{
    [Required, StringLength(256)]
    public string Name { get; init; } = string.Empty;

    [StringLength(512)]
    public string? Description { get; init; }

    [Range(1, long.MaxValue)]
    public long PermissionAId { get; init; }

    [Range(1, long.MaxValue)]
    public long PermissionBId { get; init; }
}

public class SoDRuleUpdate : SoDRuleCreate
{
    public bool? IsActive { get; init; }
}

public class SecurityStatus
{
    public EffectivePolicy Policy { get; init; } = null!;
    public bool MustChangePassword { get; init; }
    public bool MfaSetupRequired { get; init; }
    public bool EmailConfirmationRequired { get; init; }
}
