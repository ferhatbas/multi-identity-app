using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Auth;
using FSIdentity.Identity.Authorization;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Users;

public class SessionRead
{
    public long Id { get; init; }
    public long UserId { get; init; }
    public long? CompanyId { get; init; }
    public long? ApplicationTypeId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime LastSeenAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? RevokedAt { get; init; }
    public string? RevokedReason { get; init; }
    public long? ImpersonatorUserId { get; init; }

    public bool IsCurrent { get; init; }

    public bool IsOpen { get; init; }
}

public record TemporaryPassword(string Password);

public class ImpersonateRequest
{
    [System.ComponentModel.DataAnnotations.Range(1, long.MaxValue)]
    public long UserId { get; init; }

    [System.ComponentModel.DataAnnotations.StringLength(512)]
    public string? Reason { get; init; }
}

public interface IUserAdminService
{
    Task<TemporaryPassword> ResetPasswordAsync(long userId, bool callerIsPlatformAdmin, CancellationToken cancellationToken);

    Task UnlockAsync(long userId, bool callerIsPlatformAdmin, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionRead>> SessionsAsync(long userId, CancellationToken cancellationToken);

    Task<List<SessionRead>> ListAsync(long userId, long? currentSessionId, CancellationToken cancellationToken);

    Task<int> RevokeSessionsAsync(long userId, long? sessionId, bool callerIsPlatformAdmin, CancellationToken cancellationToken);
}

public class UserAdminService(
    AppDbContext db,
    IUserProvisioning provisioning,
    IPasswordHasher hasher,
    IUserAdministrationGuard guard,
    IEmailService email,
    EmailTemplates templates,
    TimeProvider clock) : IUserAdminService
{
    public async Task<TemporaryPassword> ResetPasswordAsync(long userId, bool callerIsPlatformAdmin, CancellationToken cancellationToken)
    {
        var user = await TargetAsync(userId, callerIsPlatformAdmin, cancellationToken);
        var now = Now();
        var password = provisioning.GenerateTemporaryPassword();

        user.PasswordHash = hasher.Hash(password);
        user.LastPasswordChangeAt = now;
        user.ShouldChangePasswordOnNextLogin = true;
        await EndSessionsAsync(userId, null, "password reset by an administrator", cancellationToken);
        await LiftLockoutsAsync(userId, now, cancellationToken);

        db.RecordAudit("User", userId.ToString(), "AdminPasswordReset");
        await db.SaveChangesAsync(cancellationToken);

        await email.SendAsync(templates.PasswordChanged(user), cancellationToken);
        return new TemporaryPassword(password);
    }

    public async Task UnlockAsync(long userId, bool callerIsPlatformAdmin, CancellationToken cancellationToken)
    {
        await TargetAsync(userId, callerIsPlatformAdmin, cancellationToken);
        var lifted = await LiftLockoutsAsync(userId, Now(), cancellationToken);

        db.RecordAudit("User", userId.ToString(), "Unlock", $"{{\"lockouts\":{lifted}}}");
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessionRead>> SessionsAsync(long userId, CancellationToken cancellationToken)
    {
        await TargetAsync(userId, callerIsPlatformAdmin: true, cancellationToken, requireManageRights: false);
        return await ListAsync(userId, currentSessionId: null, cancellationToken);
    }

    public async Task<int> RevokeSessionsAsync(long userId, long? sessionId, bool callerIsPlatformAdmin, CancellationToken cancellationToken)
    {
        await TargetAsync(userId, callerIsPlatformAdmin, cancellationToken);
        var ended = await EndSessionsAsync(userId, sessionId, "ended by an administrator", cancellationToken);
        if (sessionId is not null && ended == 0)
            throw new NotFoundException($"Session {sessionId} not found");

        db.RecordAudit("User", userId.ToString(), "SessionsRevoked", $"{{\"sessions\":{ended}}}");
        await db.SaveChangesAsync(cancellationToken);
        return ended;
    }

    public async Task<List<SessionRead>> ListAsync(long userId, long? currentSessionId, CancellationToken cancellationToken)
    {
        var now = Now();
        return await db.UserSessions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastSeenAt).ThenByDescending(s => s.Id)
            .Take(100)
            .Select(s => new SessionRead
            {
                Id = s.Id,
                UserId = s.UserId,
                CompanyId = s.CompanyId,
                ApplicationTypeId = s.ApplicationTypeId,
                IpAddress = s.IpAddress,
                UserAgent = s.UserAgent,
                CreatedAt = s.CreatedAt,
                LastSeenAt = s.LastSeenAt,
                ExpiresAt = s.ExpiresAt,
                RevokedAt = s.RevokedAt,
                RevokedReason = s.RevokedReason,
                ImpersonatorUserId = s.ImpersonatorUserId,
                IsCurrent = s.Id == currentSessionId,
                IsOpen = s.RevokedAt == null && s.ExpiresAt > now,
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<int> EndSessionsAsync(long userId, long? sessionId, string reason, CancellationToken cancellationToken)
    {
        var now = Now();
        var open = await db.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && (sessionId == null || s.Id == sessionId))
            .ToListAsync(cancellationToken);

        foreach (var session in open)
        {
            session.RevokedAt = now;
            session.RevokedReason = reason;
        }

        return open.Count;
    }

    private async Task<int> LiftLockoutsAsync(long userId, DateTime now, CancellationToken cancellationToken)
    {
        var active = await db.UserLockouts.Where(l => l.UserId == userId && l.LockedUntil > now).ToListAsync(cancellationToken);
        foreach (var lockout in active)
            lockout.LockedUntil = now;
        return active.Count;
    }

    private async Task<User> TargetAsync(long userId, bool callerIsPlatformAdmin, CancellationToken cancellationToken, bool requireManageRights = true)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new NotFoundException($"User {userId} not found");

        if (requireManageRights)
            await guard.EnsureCanAdministerAsync(userId, allowSelf: false, cancellationToken);

        return user;
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
