using FSIdentity.Domain.Crud;
using FSIdentity.Domain.Interfaces.MultiTenant;

namespace FSIdentity.Domain.Entities.Accounts;

[Unique(nameof(IdentityTenantId))]
public class SecurityPolicy : BaseEntity, ITenantScoped
{
    public string? Name { get; set; }

    public int? MinPasswordLength { get; set; }

    public bool? PasswordRequiresLetter { get; set; }

    public bool? PasswordRequiresDigit { get; set; }

    public bool? PasswordRequiresSymbol { get; set; }

    public bool? PasswordRequiresMixedCase { get; set; }

    public int? PasswordMaxAgeDays { get; set; }

    public int? MaxFailedAttempts { get; set; }

    public int? LockoutWindowMinutes { get; set; }

    public int? LockoutMinutes { get; set; }

    public int? SessionDays { get; set; }

    public UserType? RequireMfaFromRank { get; set; }

    public bool? RequireConfirmedEmail { get; set; }
}

[Unique(nameof(PermissionBId), Within = $"{nameof(PermissionAId)},{nameof(IdentityTenantId)}")]
public class SoDRule : BaseEntity, ITenantScoped
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Parent(typeof(Permission), InUseText = "is part of a separation-of-duties rule")]
    public long PermissionAId { get; set; }

    [Parent(typeof(Permission), InUseText = "is part of a separation-of-duties rule")]
    public long PermissionBId { get; set; }
}
