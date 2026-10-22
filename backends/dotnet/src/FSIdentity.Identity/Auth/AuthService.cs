using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Applications;
using FSIdentity.Identity.Authorization;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);

    Task LogoutAsync(long sessionId, CancellationToken cancellationToken = default);

    Task<MeResponse> MeAsync(long userId, long sessionId, CancellationToken cancellationToken = default);

    Task<AuthResponse> ChangeCompanyAsync(long userId, long sessionId, long companyId, CancellationToken cancellationToken = default);

    Task<AuthResponse> ChangeApplicationAsync(long userId, long sessionId, long applicationTypeId, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(long userId, long sessionId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    Task<MyLanguage> SetLanguageAsync(long userId, long sessionId, string? languageCode, CancellationToken cancellationToken = default);

    Task RevokeOwnSessionAsync(long userId, long sessionId, CancellationToken cancellationToken = default);

    Task LogoutEverywhereAsync(long userId, CancellationToken cancellationToken = default);

    Task<AuthResponse> ImpersonateAsync(long adminId, bool adminIsPlatformAdmin, long targetUserId, CancellationToken cancellationToken = default, string? reason = null);
}

public class AuthService(
    AppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IApplicationScopeService applications,
    IPermissionEvaluator permissions,
    ITwoFactorService twoFactor,
    IUserAdministrationGuard guard,
    ISecurityPolicyService policies,
    IOptions<JwtOptions> jwt,
    TimeProvider clock,
    IAuditContext? context = null) : IAuthService
{
    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    private const string InvalidCredentials = "Invalid user name or password";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var typed = request.UserName.Trim();
        var lookup = typed.ToLowerInvariant();

        var user = await Users().FirstOrDefaultAsync(u => u.UserName == lookup || u.Email == lookup, cancellationToken);
        if (user is null || !user.IsActive)
        {
            hasher.BurnTime();
            await RecordAttemptAsync(typed, user?.Id, succeeded: false, user is null ? "UnknownUser" : "Inactive", cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        var lockedUntil = await db.UserLockouts
            .Where(l => l.UserId == user.Id && l.LockedUntil > now)
            .MaxAsync(l => (DateTime?)l.LockedUntil, cancellationToken);
        if (lockedUntil is { } until)
        {
            await RecordAttemptAsync(typed, user.Id, succeeded: false, "Locked", cancellationToken);
            throw new AccountLockedException($"The account is locked until {until:u}");
        }

        if (!hasher.Verify(request.Password, user.PasswordHash))
        {
            await RecordAttemptAsync(typed, user.Id, succeeded: false, "WrongPassword", cancellationToken);
            await LockIfTooManyFailuresAsync(user, cancellationToken);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (user.TwoFactorEnabled)
            return new AuthResponse { TwoFactorRequired = true, TwoFactorToken = tokens.CreateTwoFactorToken(user.Id) };

        await RecordAttemptAsync(typed, user.Id, succeeded: true, null, cancellationToken);
        return await StartSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var userId = await tokens.ReadTwoFactorTokenAsync(request.TwoFactorToken)
                     ?? throw new UnauthorizedException("The sign-in has expired; start again");
        var user = await Users().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || !user.TwoFactorEnabled)
            throw new UnauthorizedException(InvalidCredentials);

        if (await db.UserLockouts.AnyAsync(l => l.UserId == user.Id && l.LockedUntil > now, cancellationToken))
        {
            await RecordAttemptAsync(user.UserName, user.Id, succeeded: false, "Locked", cancellationToken);
            throw new AccountLockedException("The account is locked");
        }

        if (!await twoFactor.VerifyAsync(user, request.Code, cancellationToken))
        {
            await RecordAttemptAsync(user.UserName, user.Id, succeeded: false, "TwoFactorFailed", cancellationToken);
            await LockIfTooManyFailuresAsync(user, cancellationToken);
            throw new UnauthorizedException("The code is not right");
        }

        await RecordAttemptAsync(user.UserName, user.Id, succeeded: true, null, cancellationToken);
        return await StartSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> StartSessionAsync(User user, CancellationToken cancellationToken)
    {
        var now = Now();
        var memberships = await MembershipsAsync(user.Id, cancellationToken);
        var companyId = memberships.Any(m => m.CompanyId == user.ActiveCompanyId)
            ? user.ActiveCompanyId
            : memberships.Select(m => (long?)m.CompanyId).FirstOrDefault();
        var applicationId = await HomeApplicationAsync(companyId, user.ActiveApplicationTypeId, cancellationToken);
        var policy = await policies.ForTenantAsync(user.IdentityTenantId, cancellationToken);

        Remember(user, companyId, applicationId);

        var session = new UserSession
        {
            UserId = user.Id,
            CompanyId = companyId,
            ApplicationTypeId = applicationId,
            IpAddress = context?.IpAddress,
            UserAgent = context?.UserAgent,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(policy.SessionDays),
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var refresh = await NewRefreshTokenAsync(user.Id, session.Id, session.ExpiresAt, cancellationToken);
        return await ResponseAsync(user, memberships, companyId, applicationId, session.Id, refresh, cancellationToken: cancellationToken);
    }

    private async Task LockIfTooManyFailuresAsync(User user, CancellationToken cancellationToken)
    {
        var options = await policies.ForTenantAsync(user.IdentityTenantId, cancellationToken);
        var now = Now();

        var lastSuccess = await db.LoginAttempts
            .Where(a => a.UserId == user.Id && a.Succeeded)
            .MaxAsync(a => (DateTime?)a.CreatedAt, cancellationToken);
        var lastLock = await db.UserLockouts.IgnoreQueryFilters()
            .Where(l => l.UserId == user.Id)
            .MaxAsync(l => (DateTime?)l.LockedAt, cancellationToken);
        var cutoff = new[] { now.AddMinutes(-options.LockoutWindowMinutes), lastSuccess ?? DateTime.MinValue, lastLock ?? DateTime.MinValue }.Max();

        var failures = await db.LoginAttempts
            .CountAsync(a => a.UserId == user.Id && !a.Succeeded && a.CreatedAt > cutoff, cancellationToken);
        if (failures < options.MaxFailedAttempts)
            return;

        db.UserLockouts.Add(new UserLockout
        {
            UserId = user.Id,
            LockedAt = now,
            LockedUntil = now.AddMinutes(options.LockoutMinutes),
            Reason = $"{failures} failed sign-in attempts",
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordAttemptAsync(string typed, long? userId, bool succeeded, string? reason, CancellationToken cancellationToken)
    {
        db.LoginAttempts.Add(new LoginAttempt
        {
            UserName = typed.Length > 256 ? typed[..256] : typed,
            UserId = userId,
            Succeeded = succeeded,
            Reason = reason,
            IpAddress = context?.IpAddress,
            UserAgent = context?.UserAgent,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var hash = tokens.Hash(refreshToken);

        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
                    ?? throw new UnauthorizedException("The refresh token is not valid");
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == token.SessionId, cancellationToken);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
            throw new UnauthorizedException("The session has ended");

        var claimed = await db.RefreshTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (claimed == 0)
        {
            await RevokeSessionAsync(session, "refresh token reuse", cancellationToken);
            throw new UnauthorizedException("The refresh token was already used; sign in again");
        }

        if (token.ExpiresAt <= now)
            throw new UnauthorizedException("The refresh token has expired");

        var user = await Users().FirstOrDefaultAsync(u => u.Id == token.UserId, cancellationToken);
        var locked = user is not null && await db.UserLockouts.AnyAsync(l => l.UserId == user.Id && l.LockedUntil > now, cancellationToken);
        if (user is null || !user.IsActive || locked)
        {
            await RevokeSessionAsync(session, "user no longer allowed", cancellationToken);
            throw new UnauthorizedException("The session has ended");
        }

        var memberships = await MembershipsAsync(user.Id, cancellationToken);
        var companyId = memberships.Any(m => m.CompanyId == session.CompanyId)
            ? session.CompanyId
            : memberships.Select(m => (long?)m.CompanyId).FirstOrDefault();
        var applicationId = companyId is { } company
            ? await applications.ResolveForSessionAsync(company, session.ApplicationTypeId, user.ActiveApplicationTypeId, cancellationToken)
            : null;

        session.CompanyId = companyId;
        session.ApplicationTypeId = applicationId;
        session.LastSeenAt = now;
        session.ExpiresAt = now.AddDays((await policies.ForTenantAsync(user.IdentityTenantId, cancellationToken)).SessionDays);
        await db.SaveChangesAsync(cancellationToken);

        var next = await NewRefreshTokenAsync(user.Id, session.Id, session.ExpiresAt, cancellationToken);
        return await ResponseAsync(user, memberships, companyId, applicationId, session.Id, next, cancellationToken: cancellationToken);
    }

    public async Task LogoutAsync(long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is not null)
            await RevokeSessionAsync(session, "logout", cancellationToken);
    }

    private async Task RevokeSessionAsync(UserSession session, string reason, CancellationToken cancellationToken)
    {
        if (session.RevokedAt is not null)
            return;

        session.RevokedAt = Now();
        session.RevokedReason = reason;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MeResponse> MeAsync(long userId, long sessionId, CancellationToken cancellationToken = default)
    {
        var user = await Users().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new UnauthorizedException("The user no longer exists");
        var session = await ActiveSessionAsync(sessionId, userId, cancellationToken);

        var memberships = await MembershipsAsync(userId, cancellationToken);
        var modules = await applications.GetModuleCodesForApplicationAsync(session.ApplicationTypeId, cancellationToken);

        return new MeResponse
        {
            User = Summary(user, memberships, session.CompanyId, session.ApplicationTypeId, await EffectiveLanguageAsync(user, session.CompanyId, cancellationToken)),
            Modules = modules.OrderBy(m => m, StringComparer.Ordinal).ToList(),
            Permissions = (await permissions.GetAllAsync(cancellationToken)).OrderBy(p => p, StringComparer.Ordinal).ToList(),
        };
    }

    public async Task<AuthResponse> ChangeCompanyAsync(
        long userId, long sessionId, long companyId, CancellationToken cancellationToken = default)
    {
        var user = await Users().FirstAsync(u => u.Id == userId, cancellationToken);
        var memberships = await MembershipsAsync(userId, cancellationToken);

        var allowed = memberships.Any(m => m.CompanyId == companyId)
                      || (user.IsPlatformAdmin
                          && await db.Companies.IgnoreQueryFilters(AcrossTenants).AnyAsync(c => c.Id == companyId && c.IsActive && !c.IsDeleted, cancellationToken));
        if (!allowed)
            throw new ForbiddenException("You are not a member of that company");

        var applicationId = await HomeApplicationAsync(companyId, user.ActiveApplicationTypeId, cancellationToken);
        Remember(user, companyId, applicationId);

        var session = await ActiveSessionAsync(sessionId, userId, cancellationToken);
        session.CompanyId = companyId;
        session.ApplicationTypeId = applicationId;
        await db.SaveChangesAsync(cancellationToken);

        return await ResponseAsync(user, memberships, companyId, applicationId, sessionId, refreshToken: null, cancellationToken: cancellationToken);
    }

    public async Task<AuthResponse> ChangeApplicationAsync(
        long userId, long sessionId, long applicationTypeId, CancellationToken cancellationToken = default)
    {
        var user = await Users().FirstAsync(u => u.Id == userId, cancellationToken);
        var session = await ActiveSessionAsync(sessionId, userId, cancellationToken);
        var companyId = session.CompanyId ?? throw new BadRequestException("Choose a company first");

        var allowed = await applications.GetAllowedForCompanyAsync(companyId, cancellationToken);
        if (allowed.All(a => a.Id != applicationTypeId))
            throw new ForbiddenException("The company's type does not allow that application");

        Remember(user, companyId, applicationTypeId);
        session.ApplicationTypeId = applicationTypeId;
        await db.SaveChangesAsync(cancellationToken);

        return await ResponseAsync(user, await MembershipsAsync(userId, cancellationToken), companyId, applicationTypeId, sessionId, refreshToken: null, cancellationToken: cancellationToken);
    }

    public async Task ChangePasswordAsync(
        long userId, long sessionId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await Users().FirstAsync(u => u.Id == userId, cancellationToken);
        if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new BadRequestException("The current password is wrong");

        hasher.EnsureAcceptable(request.NewPassword, await policies.ForTenantAsync(user.IdentityTenantId, cancellationToken));
        if (hasher.Verify(request.NewPassword, user.PasswordHash))
            throw new BadRequestException("The new password must differ from the current one");

        var now = Now();
        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.LastPasswordChangeAt = now;
        user.ShouldChangePasswordOnNextLogin = false;

        var others = await db.UserSessions
            .Where(s => s.UserId == userId && s.Id != sessionId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var other in others)
        {
            other.RevokedAt = now;
            other.RevokedReason = "password changed";
        }

        db.RecordAudit("User", userId.ToString(), "PasswordChanged");

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MyLanguage> SetLanguageAsync(long userId, long sessionId, string? languageCode, CancellationToken cancellationToken = default)
    {
        var user = await Users().FirstAsync(u => u.Id == userId, cancellationToken);
        var session = await ActiveSessionAsync(sessionId, userId, cancellationToken);

        var code = string.IsNullOrWhiteSpace(languageCode) ? null : languageCode.Trim().ToLowerInvariant();
        if (code is not null && !await db.Languages.IgnoreQueryFilters(AcrossTenants).AnyAsync(l => l.Code == code && l.IsActive && !l.IsDeleted, cancellationToken))
            throw new BadRequestException("That language is not available");

        user.LanguageCode = code;
        db.RecordAudit("User", userId.ToString(), "LanguageChanged");
        await db.SaveChangesAsync(cancellationToken);

        return new MyLanguage { LanguageCode = code, EffectiveLanguageCode = await EffectiveLanguageAsync(user, session.CompanyId, cancellationToken) };
    }

    private async Task<string> EffectiveLanguageAsync(User user, long? companyId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(user.LanguageCode)) return user.LanguageCode;

        if (companyId is { } id)
        {
            var company = await db.Companies.IgnoreQueryFilters(AcrossTenants).Where(c => c.Id == id).Select(c => c.LanguageCode).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(company)) return company;
        }

        if (user.IdentityTenantId is { } tenantId)
        {
            var tenant = await db.Tenants.IgnoreQueryFilters(AcrossTenants).Where(t => t.Id == tenantId).Select(t => t.LanguageCode).FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(tenant)) return tenant;
        }

        return await db.Languages.IgnoreQueryFilters(AcrossTenants).Where(l => l.IsFallback && l.IsActive && !l.IsDeleted).Select(l => l.Code).FirstOrDefaultAsync(cancellationToken) ?? "en";
    }

    public async Task RevokeOwnSessionAsync(long userId, long sessionId, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken)
                      ?? throw new NotFoundException($"Session {sessionId} not found");
        await RevokeSessionAsync(session, "ended by the user", cancellationToken);
    }

    public async Task LogoutEverywhereAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = Now();
        foreach (var session in await db.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToListAsync(cancellationToken))
        {
            session.RevokedAt = now;
            session.RevokedReason = "signed out everywhere";
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuthResponse> ImpersonateAsync(
        long adminId, bool adminIsPlatformAdmin, long targetUserId, CancellationToken cancellationToken = default, string? reason = null)
    {
        if (targetUserId == adminId)
            throw new BadRequestException("You cannot act as yourself");

        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId, cancellationToken)
                     ?? throw new NotFoundException($"User {targetUserId} not found");
        if (!target.IsActive)
            throw new BadRequestException("The user is switched off");
        if (target.IsPlatformAdmin && !adminIsPlatformAdmin)
            throw new ForbiddenException("Only a platform administrator may act as a platform administrator");
        await guard.EnsureCanAdministerAsync(target.Id, allowSelf: false, cancellationToken);

        var now = Now();
        var memberships = await MembershipsAsync(target.Id, cancellationToken);
        var companyId = memberships.Any(m => m.CompanyId == target.ActiveCompanyId)
            ? target.ActiveCompanyId
            : memberships.Select(m => (long?)m.CompanyId).FirstOrDefault();
        var applicationId = await HomeApplicationAsync(companyId, target.ActiveApplicationTypeId, cancellationToken);

        var session = new UserSession
        {
            UserId = target.Id,
            CompanyId = companyId,
            ApplicationTypeId = applicationId,
            IpAddress = context?.IpAddress,
            UserAgent = context?.UserAgent,
            LastSeenAt = now,
            ExpiresAt = now.AddMinutes(jwt.Value.AccessMinutes),
            ImpersonatorUserId = adminId,
        };
        db.UserSessions.Add(session);
        db.RecordAudit("User", target.Id.ToString(), "Impersonate", System.Text.Json.JsonSerializer.Serialize(new { by = adminId, reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim() }));
        await db.SaveChangesAsync(cancellationToken);

        return await ResponseAsync(target, memberships, companyId, applicationId, session.Id, refreshToken: null, impersonatorId: adminId, cancellationToken: cancellationToken);
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private IQueryable<User> Users() => db.Users.IgnoreQueryFilters(AcrossTenants);

    private async Task<UserSession> ActiveSessionAsync(long sessionId, long userId, CancellationToken cancellationToken)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId, cancellationToken);
        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= Now())
            throw new UnauthorizedException("The session has ended");
        return session;
    }

    private async Task<long?> HomeApplicationAsync(long? companyId, long? remembered, CancellationToken cancellationToken)
        => companyId is { } company ? await applications.ResolveActiveAsync(company, remembered, cancellationToken) : null;

    private static void Remember(User user, long? companyId, long? applicationId)
    {
        if (user.ActiveCompanyId != companyId)
            user.ActiveCompanyId = companyId;
        if (user.ActiveApplicationTypeId != applicationId)
            user.ActiveApplicationTypeId = applicationId;
    }

    private async Task<string> NewRefreshTokenAsync(long userId, long sessionId, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var value = tokens.NewRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            SessionId = sessionId,
            TokenHash = tokens.Hash(value),
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync(cancellationToken);
        return value;
    }

    private async Task<List<CompanyMembership>> MembershipsAsync(long userId, CancellationToken cancellationToken)
        => await (from m in db.UserCompanies.IgnoreQueryFilters(AcrossTenants)
                  join c in db.Companies.IgnoreQueryFilters(AcrossTenants) on m.CompanyId equals c.Id
                  where m.UserId == userId && m.IsActive && c.IsActive
                  orderby c.Id
                  select new CompanyMembership(c.Id, c.Name, c.Code, m.MembershipType))
            .ToListAsync(cancellationToken);

    private async Task<AuthResponse> ResponseAsync(
        User user, List<CompanyMembership> memberships, long? companyId, long? applicationId, long sessionId, string? refreshToken,
        long? impersonatorId = null, CancellationToken cancellationToken = default)
    {
        var restrictions = impersonatorId is null ? await RestrictionsAsync(user, memberships, cancellationToken) : SessionRestrictions.None;
        var access = tokens.CreateAccessToken(user, companyId, applicationId, sessionId, impersonatorId, restrictions);
        return new AuthResponse
        {
            AccessToken = access.Value,
            RefreshToken = refreshToken,
            ExpiresAt = access.ExpiresAt,
            User = Summary(user, memberships, companyId, applicationId, await EffectiveLanguageAsync(user, companyId, cancellationToken), restrictions),
        };
    }

    private async Task<SessionRestrictions> RestrictionsAsync(User user, List<CompanyMembership> memberships, CancellationToken cancellationToken)
    {
        var policy = await policies.ForTenantAsync(user.IdentityTenantId, cancellationToken);

        var expired = policy.PasswordMaxAgeDays is { } days && user.LastPasswordChangeAt is { } changed && changed.AddDays(days) <= Now();
        var rank = user.IsPlatformAdmin
            ? UserType.PlatformAdmin
            : memberships.Select(m => m.MembershipType).DefaultIfEmpty(UserType.Standard).Max();

        return new SessionRestrictions(
            PasswordChange: user.ShouldChangePasswordOnNextLogin || expired,
            MfaSetup: policy.RequireMfaFromRank is { } required && !user.TwoFactorEnabled && rank >= required,
            EmailConfirmation: policy.RequireConfirmedEmail && !user.EmailConfirmed);
    }

    private static UserSummary Summary(
        User user, List<CompanyMembership> memberships, long? companyId, long? applicationId, string language, SessionRestrictions? restrictions = null) => new()
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        LanguageCode = user.LanguageCode,
        EffectiveLanguageCode = language,
        IsPlatformAdmin = user.IsPlatformAdmin,
        TenantId = user.IdentityTenantId,
        ActiveCompanyId = companyId,
        ActiveApplicationTypeId = applicationId,
        TwoFactorEnabled = user.TwoFactorEnabled,
        MustChangePassword = restrictions?.PasswordChange ?? user.ShouldChangePasswordOnNextLogin,
        MfaSetupRequired = restrictions?.MfaSetup ?? false,
        EmailConfirmationRequired = restrictions?.EmailConfirmation ?? false,
        Companies = memberships,
    };
}
