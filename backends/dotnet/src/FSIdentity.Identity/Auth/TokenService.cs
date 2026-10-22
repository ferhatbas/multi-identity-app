using System.Security.Cryptography;
using System.Text;
using FSIdentity.Domain.Entities.Accounts;
using FSIdentity.Domain.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace FSIdentity.Identity.Auth;

public record AccessToken(string Value, DateTime ExpiresAt);

public record SessionRestrictions(bool PasswordChange, bool MfaSetup, bool EmailConfirmation)
{
    public static SessionRestrictions None { get; } = new(false, false, false);
}

public interface ITokenService
{
    AccessToken CreateAccessToken(
        User user, long? companyId, long? applicationTypeId, long sessionId, long? impersonatorId = null, SessionRestrictions? restrictions = null);

    string NewRefreshToken();

    string Hash(string refreshToken);

    string CreateTwoFactorToken(long userId);

    Task<long?> ReadTwoFactorTokenAsync(string token);
}

public class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JsonWebTokenHandler _handler = new();

    public static SymmetricSecurityKey SigningKey(JwtOptions options) => new(Encoding.UTF8.GetBytes(options.Key));

    public AccessToken CreateAccessToken(
        User user, long? companyId, long? applicationTypeId, long sessionId, long? impersonatorId = null, SessionRestrictions? restrictions = null)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(jwt.AccessMinutes);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
            [ClaimNames.UserId] = user.Id.ToString(),
            [ClaimNames.UserName] = user.UserName,
            [ClaimNames.IsSuperAdmin] = user.IsPlatformAdmin ? "true" : "false",
            [ClaimNames.SessionId] = sessionId.ToString(),
        };
        if (user.IdentityTenantId is { } tenantId)
            claims[ClaimNames.TenantId] = tenantId.ToString();
        if (companyId is { } company)
            claims[ClaimNames.ActiveCompanyId] = company.ToString();
        if (applicationTypeId is { } application)
            claims[ClaimNames.ActiveApplicationTypeId] = application.ToString();
        restrictions ??= new SessionRestrictions(user.ShouldChangePasswordOnNextLogin, false, false);
        if (restrictions.PasswordChange)
            claims[ClaimNames.MustChangePassword] = "true";
        if (restrictions.MfaSetup)
            claims[ClaimNames.MfaSetupRequired] = "true";
        if (restrictions.EmailConfirmation)
            claims[ClaimNames.EmailConfirmationRequired] = "true";
        if (impersonatorId is { } impersonator)
            claims[ClaimNames.ImpersonatorId] = impersonator.ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Claims = claims,
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expires);
    }

    private const string TwoFactorAudienceSuffix = ".2fa";

    public string CreateTwoFactorToken(long userId)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience + TwoFactorAudienceSuffix,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(5),
            Claims = new Dictionary<string, object> { [ClaimNames.UserId] = userId.ToString() },
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }

    public async Task<long?> ReadTwoFactorTokenAsync(string token)
    {
        var jwt = options.Value;
        var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience + TwoFactorAudienceSuffix,
            IssuerSigningKey = SigningKey(jwt),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(10),
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = clock.GetUtcNow().UtcDateTime;
                return (notBefore is null || notBefore <= now.AddSeconds(10)) && (expires is null || expires > now.AddSeconds(-10));
            },
        });

        return result.IsValid && result.Claims.TryGetValue(ClaimNames.UserId, out var id) && long.TryParse(id?.ToString(), out var userId)
            ? userId
            : null;
    }

    public string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public string Hash(string refreshToken) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
