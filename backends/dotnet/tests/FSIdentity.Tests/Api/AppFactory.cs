using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FSIdentity.Tests.Api;

public class AppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-test-signing-key-0123456789",
                ["Passwords:WorkFactor"] = "4",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<FSIdentity.Identity.Auth.IEmailService>();
            services.AddSingleton<CapturedEmails>();
            services.AddSingleton<FSIdentity.Identity.Auth.IEmailService>(sp => sp.GetRequiredService<CapturedEmails>());

            services.RemoveAll<FSIdentity.Identity.Notifications.IPushSender>();
            services.AddSingleton<CapturedPushes>();
            services.AddSingleton<FSIdentity.Identity.Notifications.IPushSender>(sp => sp.GetRequiredService<CapturedPushes>());

            services.AddScoped<FSIdentity.Identity.Authorization.PermissionResolver>();
            services.AddScoped<FSIdentity.Identity.Authorization.PermissionEvaluator>();
            services.RemoveAll<FSIdentity.Domain.Security.IPermissionEvaluator>();
            services.AddScoped<FSIdentity.Domain.Security.IPermissionEvaluator>(sp => new TestPermissionEvaluator(
                sp.GetRequiredService<FSIdentity.Identity.Authorization.PermissionEvaluator>(),
                sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()));

            services.AddScoped<FSIdentity.Identity.Authorization.CallerAccess>();
            services.RemoveAll<FSIdentity.Domain.Security.ICallerAccess>();
            services.AddScoped<FSIdentity.Domain.Security.ICallerAccess>(sp => new TestCallerAccess(
                sp.GetRequiredService<FSIdentity.Identity.Authorization.CallerAccess>(),
                sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>()));

            services.AddSingleton<IStartupFilter, TestIdentityStartupFilter>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}

public sealed class CapturedPushes : FSIdentity.Identity.Notifications.IPushSender
{
    public List<FSIdentity.Identity.Notifications.PushMessage> Sent { get; } = [];

    public string? FailWith { get; set; }

    public Task<FSIdentity.Identity.Notifications.PushSendResult> SendAsync(
        FSIdentity.Identity.Notifications.PushMessage message, CancellationToken cancellationToken = default)
    {
        if (FailWith is not null)
            return Task.FromResult(new FSIdentity.Identity.Notifications.PushSendResult(false, Error: FailWith));

        Sent.Add(message);
        return Task.FromResult(new FSIdentity.Identity.Notifications.PushSendResult(true, $"t{Sent.Count}"));
    }
}

public sealed class CapturedEmails : FSIdentity.Identity.Auth.IEmailService
{
    private readonly List<FSIdentity.Identity.Auth.EmailMessage> _sent = [];

    public IReadOnlyList<FSIdentity.Identity.Auth.EmailMessage> Sent
    {
        get { lock (_sent) return _sent.ToList(); }
    }

    public Task SendAsync(FSIdentity.Identity.Auth.EmailMessage message, CancellationToken cancellationToken = default)
    {
        lock (_sent) _sent.Add(message);
        return Task.CompletedTask;
    }

    public string TokenFor(string address)
    {
        var message = Sent.Last(m => string.Equals(m.To, address, StringComparison.OrdinalIgnoreCase));
        var match = System.Text.RegularExpressions.Regex.Match(message.Text, @"token=([^\s&]+)");
        return Uri.UnescapeDataString(match.Groups[1].Value);
    }
}

internal sealed class TestCallerAccess(
    FSIdentity.Identity.Authorization.CallerAccess real,
    Microsoft.AspNetCore.Http.IHttpContextAccessor http) : FSIdentity.Domain.Security.ICallerAccess
{
    public async Task<FSIdentity.Domain.Entities.Accounts.UserType> RankAsync(CancellationToken cancellationToken = default)
    {
        var declared = http.HttpContext?.User.FindFirst(TestIdentityStartupFilter.RankClaim)?.Value;
        if (declared is not null && http.HttpContext!.User.FindFirst(FSIdentity.Domain.Security.ClaimNames.IsSuperAdmin)?.Value != "true")
            return (FSIdentity.Domain.Entities.Accounts.UserType)int.Parse(declared);

        return await real.RankAsync(cancellationToken);
    }
}

internal sealed class TestPermissionEvaluator(
    FSIdentity.Identity.Authorization.PermissionEvaluator real,
    Microsoft.AspNetCore.Http.IHttpContextAccessor http) : FSIdentity.Domain.Security.IPermissionEvaluator
{
    private string? Declared => http.HttpContext?.User.FindFirst(TestIdentityStartupFilter.PermissionsClaim)?.Value;

    public async Task<bool> HasAsync(string permissionCode, CancellationToken cancellationToken = default)
        => Declared is null
            ? await real.HasAsync(permissionCode, cancellationToken)
            : Declared == "*" || Declared.Split(',').Contains(permissionCode, StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlySet<string>> GetAllAsync(CancellationToken cancellationToken = default)
        => Declared is null
            ? await real.GetAllAsync(cancellationToken)
            : Declared == "*"
                ? new HashSet<string>(FSIdentity.Domain.Security.Permissions.All)
                : new HashSet<string>(Declared.Split(',', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
}
