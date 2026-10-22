using System.ComponentModel.DataAnnotations;
using FSIdentity.Domain.Audit;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Data;
using FSIdentity.Identity.Security;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Identity.Auth;

public class ForgotPasswordRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; init; } = string.Empty;
}

public class ResetPasswordRequest
{
    [Required, StringLength(256)]
    public string Token { get; init; } = string.Empty;

    [Required, StringLength(256)]
    public string NewPassword { get; init; } = string.Empty;
}

public class ConfirmEmailRequest
{
    [Required, StringLength(256)]
    public string Token { get; init; } = string.Empty;
}

public interface IAccountRecoveryService
{
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task SendEmailConfirmationAsync(long userId, CancellationToken cancellationToken = default);

    Task ConfirmEmailAsync(string token, CancellationToken cancellationToken = default);
}

public class AccountRecoveryService(
    AppDbContext db,
    ITokenService tokens,
    IPasswordHasher hasher,
    ISecurityPolicyService policies,
    IEmailService email,
    EmailTemplates templates,
    TimeProvider clock,
    IAuditContext? context = null) : IAccountRecoveryService
{
    public const int ResetValidMinutes = 60;
    public const int ConfirmationValidHours = 24;

    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    private const string InvalidLink = "The link is invalid or has expired";

    public async Task RequestPasswordResetAsync(string address, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var lookup = address.Trim().ToLowerInvariant();
        var user = await db.Users.IgnoreQueryFilters(AcrossTenants).FirstOrDefaultAsync(u => u.Email == lookup && u.IsActive, cancellationToken);
        if (user is null)
            return;

        if (await db.PasswordResets.AnyAsync(r => r.UserId == user.Id && r.CreatedAt > now.AddMinutes(-1), cancellationToken))
            return;

        await ExpireOpenResetsAsync(user.Id, now, cancellationToken);

        var token = tokens.NewRefreshToken();
        db.PasswordResets.Add(new PasswordReset
        {
            UserId = user.Id,
            TokenHash = tokens.Hash(token),
            ExpiresAt = now.AddMinutes(ResetValidMinutes),
            IpAddress = context?.IpAddress,
        });
        await db.SaveChangesAsync(cancellationToken);

        await email.SendAsync(templates.PasswordReset(user, token, ResetValidMinutes), cancellationToken);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var hash = tokens.Hash(request.Token);
        var reset = await db.PasswordResets.FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
        if (reset is null || reset.UsedAt is not null || reset.ExpiresAt <= now)
            throw new BadRequestException(InvalidLink);

        var user = await db.Users.IgnoreQueryFilters(AcrossTenants).FirstOrDefaultAsync(u => u.Id == reset.UserId && u.IsActive, cancellationToken)
                   ?? throw new BadRequestException(InvalidLink);

        hasher.EnsureAcceptable(request.NewPassword, await policies.ForTenantAsync(user.IdentityTenantId, cancellationToken));

        user.PasswordHash = hasher.Hash(request.NewPassword);
        user.LastPasswordChangeAt = now;
        user.ShouldChangePasswordOnNextLogin = false;
        await ExpireOpenResetsAsync(user.Id, now, cancellationToken);
        reset.UsedAt = now;

        foreach (var session in await db.UserSessions.Where(s => s.UserId == user.Id && s.RevokedAt == null).ToListAsync(cancellationToken))
        {
            session.RevokedAt = now;
            session.RevokedReason = "password reset";
        }
        foreach (var lockout in await db.UserLockouts.Where(l => l.UserId == user.Id && l.LockedUntil > now).ToListAsync(cancellationToken))
            lockout.LockedUntil = now;

        db.RecordAudit("User", user.Id.ToString(), "PasswordReset");
        await db.SaveChangesAsync(cancellationToken);
        await email.SendAsync(templates.PasswordChanged(user), cancellationToken);
    }

    public async Task SendEmailConfirmationAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.IgnoreQueryFilters(AcrossTenants).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
                   ?? throw new UnauthorizedException("The user no longer exists");
        if (user.EmailConfirmed)
            throw new ConflictException("The e-mail address is already confirmed");

        var token = tokens.NewRefreshToken();
        db.EmailConfirmations.Add(new EmailConfirmation
        {
            UserId = user.Id,
            Email = user.Email,
            TokenHash = tokens.Hash(token),
            ExpiresAt = Now().AddHours(ConfirmationValidHours),
        });
        await db.SaveChangesAsync(cancellationToken);

        await email.SendAsync(templates.EmailConfirmation(user, token), cancellationToken);
    }

    public async Task ConfirmEmailAsync(string token, CancellationToken cancellationToken = default)
    {
        var now = Now();
        var hash = tokens.Hash(token);
        var confirmation = await db.EmailConfirmations.FirstOrDefaultAsync(c => c.TokenHash == hash, cancellationToken);
        if (confirmation is null || confirmation.ConfirmedAt is not null || confirmation.ExpiresAt <= now)
            throw new BadRequestException(InvalidLink);

        var user = await db.Users.IgnoreQueryFilters(AcrossTenants).FirstOrDefaultAsync(u => u.Id == confirmation.UserId, cancellationToken);

        if (user is null || user.Email != confirmation.Email)
            throw new BadRequestException(InvalidLink);

        user.EmailConfirmed = true;
        confirmation.ConfirmedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task ExpireOpenResetsAsync(long userId, DateTime now, CancellationToken cancellationToken)
    {
        foreach (var open in await db.PasswordResets.Where(r => r.UserId == userId && r.UsedAt == null && r.ExpiresAt > now).ToListAsync(cancellationToken))
            open.ExpiresAt = now;
    }

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
