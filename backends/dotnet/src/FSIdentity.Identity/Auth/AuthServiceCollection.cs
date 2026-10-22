using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FSIdentity.Identity.Auth;

public static class AuthServiceCollection
{
    public static IServiceCollection AddIdentityAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<LoginOptions>().Bind(configuration.GetSection(LoginOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<PasswordOptions>().Bind(configuration.GetSection(PasswordOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        services.AddOptions<EmailOptions>().Bind(configuration.GetSection(EmailOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SecurityOptions>().Bind(configuration.GetSection(SecurityOptions.SectionName));

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddSingleton<EmailTemplates>();
        if (string.Equals(configuration[$"{EmailOptions.SectionName}:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IEmailService, SmtpEmailService>();
        else
            services.AddSingleton<IEmailService, LoggingEmailService>();
        services.AddScoped<IAccountRecoveryService, AccountRecoveryService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISessionValidator, SessionValidator>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new()
                {
                    ValidIssuer = settings.Issuer,
                    ValidAudience = settings.Audience,
                    IssuerSigningKey = TokenService.SigningKey(settings),
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.UserName,
                };
                bearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal?.SessionId() is not { } sessionId)
                            return;

                        var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                        if (!await validator.IsOpenAsync(sessionId, context.HttpContext.RequestAborted))
                            context.Fail("The session has ended");
                    },
                };
            });

        return services;
    }
}

public interface ISessionValidator
{
    Task<bool> IsOpenAsync(long sessionId, CancellationToken cancellationToken);
}

public class SessionValidator(AppDbContext db, TimeProvider clock) : ISessionValidator
{
    public Task<bool> IsOpenAsync(long sessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.UserSessions.AnyAsync(s => s.Id == sessionId && s.RevokedAt == null && s.ExpiresAt > now, cancellationToken);
    }
}
