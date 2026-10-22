using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Identity.Auth;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Security;

public record EffectivePolicy(
    int MinPasswordLength,
    bool PasswordRequiresLetter,
    bool PasswordRequiresDigit,
    bool PasswordRequiresSymbol,
    bool PasswordRequiresMixedCase,
    int? PasswordMaxAgeDays,
    int MaxFailedAttempts,
    int LockoutWindowMinutes,
    int LockoutMinutes,
    int SessionDays,
    UserType? RequireMfaFromRank,
    bool RequireConfirmedEmail);

public interface ISecurityPolicyService
{
    Task<EffectivePolicy> ForTenantAsync(long? tenantId, CancellationToken cancellationToken = default);
}

public class SecurityPolicyService(
    AppDbContext db,
    IOptions<PasswordOptions> passwords,
    IOptions<LoginOptions> login,
    IOptions<JwtOptions> jwt) : ISecurityPolicyService
{
    public async Task<EffectivePolicy> ForTenantAsync(long? tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await db.SecurityPolicies.IgnoreQueryFilters([FSDbContext.TenantFilter])
            .Where(p => p.IsActive && (p.IdentityTenantId == null || p.IdentityTenantId == tenantId))
            .ToListAsync(cancellationToken);

        var platform = rows.FirstOrDefault(p => p.IdentityTenantId == null);
        var tenant = tenantId is null ? null : rows.FirstOrDefault(p => p.IdentityTenantId == tenantId);

        var baseline = new EffectivePolicy(
            passwords.Value.MinLength, true, true, false, false, null,
            login.Value.MaxFailedAttempts, login.Value.WindowMinutes, login.Value.LockoutMinutes,
            jwt.Value.RefreshDays, null, false);

        var withPlatform = platform is null ? baseline : Replace(baseline, platform);
        return tenant is null ? withPlatform : Tighten(withPlatform, tenant);
    }

    private static EffectivePolicy Replace(EffectivePolicy b, SecurityPolicy p) => b with
    {
        MinPasswordLength = p.MinPasswordLength ?? b.MinPasswordLength,
        PasswordRequiresLetter = p.PasswordRequiresLetter ?? b.PasswordRequiresLetter,
        PasswordRequiresDigit = p.PasswordRequiresDigit ?? b.PasswordRequiresDigit,
        PasswordRequiresSymbol = p.PasswordRequiresSymbol ?? b.PasswordRequiresSymbol,
        PasswordRequiresMixedCase = p.PasswordRequiresMixedCase ?? b.PasswordRequiresMixedCase,
        PasswordMaxAgeDays = p.PasswordMaxAgeDays ?? b.PasswordMaxAgeDays,
        MaxFailedAttempts = p.MaxFailedAttempts ?? b.MaxFailedAttempts,
        LockoutWindowMinutes = p.LockoutWindowMinutes ?? b.LockoutWindowMinutes,
        LockoutMinutes = p.LockoutMinutes ?? b.LockoutMinutes,
        SessionDays = p.SessionDays ?? b.SessionDays,
        RequireMfaFromRank = p.RequireMfaFromRank ?? b.RequireMfaFromRank,
        RequireConfirmedEmail = p.RequireConfirmedEmail ?? b.RequireConfirmedEmail,
    };

    private static EffectivePolicy Tighten(EffectivePolicy b, SecurityPolicy p) => b with
    {
        MinPasswordLength = Math.Max(b.MinPasswordLength, p.MinPasswordLength ?? 0),
        PasswordRequiresLetter = b.PasswordRequiresLetter || p.PasswordRequiresLetter == true,
        PasswordRequiresDigit = b.PasswordRequiresDigit || p.PasswordRequiresDigit == true,
        PasswordRequiresSymbol = b.PasswordRequiresSymbol || p.PasswordRequiresSymbol == true,
        PasswordRequiresMixedCase = b.PasswordRequiresMixedCase || p.PasswordRequiresMixedCase == true,
        PasswordMaxAgeDays = Min(b.PasswordMaxAgeDays, p.PasswordMaxAgeDays),
        MaxFailedAttempts = Math.Min(b.MaxFailedAttempts, p.MaxFailedAttempts ?? int.MaxValue),
        LockoutWindowMinutes = Math.Max(b.LockoutWindowMinutes, p.LockoutWindowMinutes ?? 0),
        LockoutMinutes = Math.Max(b.LockoutMinutes, p.LockoutMinutes ?? 0),
        SessionDays = Math.Min(b.SessionDays, p.SessionDays ?? int.MaxValue),
        RequireMfaFromRank = (b.RequireMfaFromRank, p.RequireMfaFromRank) switch
        {
            (null, var tenantRank) => tenantRank,
            (var platformRank, null) => platformRank,
            var (platformRank, tenantRank) => (UserType)Math.Min((int)platformRank!.Value, (int)tenantRank!.Value),
        },
        RequireConfirmedEmail = b.RequireConfirmedEmail || p.RequireConfirmedEmail == true,
    };

    private static int? Min(int? a, int? b) => (a, b) switch
    {
        (null, var x) => x,
        (var x, null) => x,
        var (x, y) => Math.Min(x!.Value, y!.Value),
    };
}
