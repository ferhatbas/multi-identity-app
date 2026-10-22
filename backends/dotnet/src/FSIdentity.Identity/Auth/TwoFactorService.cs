using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Auth;

public class TwoFactorCodeRequest
{
    [Required, StringLength(16)]
    public string Code { get; init; } = string.Empty;
}

public class TwoFactorConfirmRequest
{
    [Required, StringLength(256)]
    public string Password { get; init; } = string.Empty;

    [Required, StringLength(32)]
    public string Code { get; init; } = string.Empty;
}

public class TwoFactorLoginRequest
{
    [Required]
    public string TwoFactorToken { get; init; } = string.Empty;

    [Required, StringLength(32)]
    public string Code { get; init; } = string.Empty;
}

public record TwoFactorSetup(string Secret, string OtpAuthUri);

public record RecoveryCodes(IReadOnlyList<string> Codes);

public interface ITwoFactorService
{
    Task<TwoFactorSetup> SetupAsync(long userId, CancellationToken cancellationToken = default);

    Task<RecoveryCodes> EnableAsync(long userId, string code, CancellationToken cancellationToken = default);

    Task DisableAsync(long userId, TwoFactorConfirmRequest request, CancellationToken cancellationToken = default);

    Task<RecoveryCodes> RegenerateRecoveryCodesAsync(long userId, TwoFactorConfirmRequest request, CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(User user, string code, CancellationToken cancellationToken = default);
}

public class TwoFactorService(
    AppDbContext db,
    ISecretProtector protector,
    IPasswordHasher hasher,
    ITokenService tokens,
    IEmailService email,
    EmailTemplates templates,
    IOptions<JwtOptions> jwt,
    TimeProvider clock) : ITwoFactorService
{
    public const int RecoveryCodeCount = 10;

    private static readonly string[] AcrossTenants = [FSDbContext.TenantFilter];

    public async Task<TwoFactorSetup> SetupAsync(long userId, CancellationToken cancellationToken = default)
    {
        var user = await UserAsync(userId, cancellationToken);
        if (user.TwoFactorEnabled)
            throw new ConflictException("Two-factor authentication is already on");

        var secret = Totp.NewSecret();
        user.TwoFactorSecret = protector.Protect(secret);
        user.TwoFactorLastStep = null;
        await db.SaveChangesAsync(cancellationToken);

        return new TwoFactorSetup(secret, Totp.Uri(jwt.Value.Issuer, user.Email, secret));
    }

    public async Task<RecoveryCodes> EnableAsync(long userId, string code, CancellationToken cancellationToken = default)
    {
        var user = await UserAsync(userId, cancellationToken);
        if (user.TwoFactorEnabled)
            throw new ConflictException("Two-factor authentication is already on");
        if (user.TwoFactorSecret is null)
            throw new BadRequestException("Start the setup first");

        var step = Totp.Verify(protector.Unprotect(user.TwoFactorSecret), code.Trim(), clock.GetUtcNow(), null)
                   ?? throw new BadRequestException("The code is not right");

        user.TwoFactorEnabled = true;
        user.TwoFactorActivatedAt = clock.GetUtcNow().UtcDateTime;
        user.TwoFactorLastStep = step;
        var codes = await NewRecoveryCodesAsync(user.Id, cancellationToken);
        db.RecordAudit("User", user.Id.ToString(), "TwoFactorEnabled");
        await db.SaveChangesAsync(cancellationToken);

        await email.SendAsync(templates.TwoFactorChanged(user, enabled: true), cancellationToken);
        return codes;
    }

    public async Task DisableAsync(long userId, TwoFactorConfirmRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ConfirmedAsync(userId, request, cancellationToken);

        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.TwoFactorActivatedAt = null;
        user.TwoFactorLastStep = null;
        foreach (var code in await db.UserRecoveryCodes.Where(c => c.UserId == userId).ToListAsync(cancellationToken))
            db.UserRecoveryCodes.Remove(code);
        db.RecordAudit("User", user.Id.ToString(), "TwoFactorDisabled");
        await db.SaveChangesAsync(cancellationToken);

        await email.SendAsync(templates.TwoFactorChanged(user, enabled: false), cancellationToken);
    }

    public async Task<RecoveryCodes> RegenerateRecoveryCodesAsync(
        long userId, TwoFactorConfirmRequest request, CancellationToken cancellationToken = default)
    {
        var user = await ConfirmedAsync(userId, request, cancellationToken);
        var codes = await NewRecoveryCodesAsync(user.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return codes;
    }

    public async Task<bool> VerifyAsync(User user, string code, CancellationToken cancellationToken = default)
    {
        if (!user.TwoFactorEnabled || user.TwoFactorSecret is null)
            return false;

        var typed = code.Trim();
        if (Totp.Verify(protector.Unprotect(user.TwoFactorSecret), typed, clock.GetUtcNow(), user.TwoFactorLastStep) is { } step)
        {
            user.TwoFactorLastStep = step;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var hash = RecoveryHash(typed);
        var recovery = await db.UserRecoveryCodes
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAt == null, cancellationToken);
        if (recovery is null)
            return false;

        recovery.UsedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<User> UserAsync(long userId, CancellationToken cancellationToken)
        => await db.Users.IgnoreQueryFilters(AcrossTenants).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
           ?? throw new UnauthorizedException("The user no longer exists");

    private async Task<User> ConfirmedAsync(long userId, TwoFactorConfirmRequest request, CancellationToken cancellationToken)
    {
        var user = await UserAsync(userId, cancellationToken);
        if (!user.TwoFactorEnabled)
            throw new ConflictException("Two-factor authentication is not on");
        if (!hasher.Verify(request.Password, user.PasswordHash))
            throw new BadRequestException("The password is wrong");
        if (!await VerifyAsync(user, request.Code, cancellationToken))
            throw new BadRequestException("The code is not right");
        return user;
    }

    private async Task<RecoveryCodes> NewRecoveryCodesAsync(long userId, CancellationToken cancellationToken)
    {
        foreach (var old in await db.UserRecoveryCodes.Where(c => c.UserId == userId).ToListAsync(cancellationToken))
            db.UserRecoveryCodes.Remove(old);

        const string alphabet = "abcdefghjkmnpqrstuvwxyz23456789";
        var codes = new List<string>();
        for (var i = 0; i < RecoveryCodeCount; i++)
        {
            var raw = new string(Enumerable.Range(0, 10).Select(_ => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)]).ToArray());
            var code = $"{raw[..5]}-{raw[5..]}";
            codes.Add(code);
            db.UserRecoveryCodes.Add(new UserRecoveryCode { UserId = userId, CodeHash = RecoveryHash(code) });
        }

        return new RecoveryCodes(codes);
    }

    private string RecoveryHash(string code)
        => tokens.Hash(new string(code.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant());
}
