using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using FSIdentity.Domain.Entities.Notifications;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Security;
using FSIdentity.Identity.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FSIdentity.Identity.Notifications;

public class PreferenceView
{
    public string Category { get; init; } = string.Empty;
    public string Channel { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public bool Mandatory { get; init; }

    public bool IsDefault { get; init; }
}

public class PreferenceWrite
{
    [Required, StringLength(32)]
    public string Category { get; init; } = string.Empty;

    [Required, StringLength(16)]
    public string Channel { get; init; } = string.Empty;

    public bool Enabled { get; init; }
}

public class PreferencesWrite
{
    [Required]
    public List<PreferenceWrite> Items { get; init; } = [];
}

public class PushTokenRegister
{
    [Required, StringLength(1024, MinimumLength = 8)]
    public string Token { get; init; } = string.Empty;

    [Required, StringLength(16)]
    public string Platform { get; init; } = string.Empty;

    [StringLength(128)]
    public string? DeviceName { get; init; }
}

public class PushTokenView
{
    public long Id { get; init; }
    public string Platform { get; init; } = string.Empty;
    public string? DeviceName { get; init; }
    public DateTime? LastSeenAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public class PushTicketView
{
    public long Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? Error { get; init; }
    public DateTime? SentAt { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public class PushRequest
{
    [Required, StringLength(32)]
    public string Category { get; init; } = NotificationCategories.System;

    [Required, StringLength(256)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(2000)]
    public string Body { get; init; } = string.Empty;
}

public sealed record PushMessage(string Token, string Platform, string Title, string Body);

public sealed record PushSendResult(bool Succeeded, string? ProviderTicketId = null, string? Error = null);

public interface IPushSender
{
    Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default);
}

public class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Push ({Platform}): {Title}", message.Platform, message.Title);
        return Task.FromResult(new PushSendResult(true, $"log-{Guid.NewGuid():N}"));
    }
}

public interface IPushNotifier
{
    Task<List<PushTicket>> NotifyAsync(long userId, string category, string title, string body, CancellationToken cancellationToken = default);
}

public interface INotificationService
{
    Task<List<PreferenceView>> PreferencesAsync(CancellationToken cancellationToken = default);

    Task<List<PreferenceView>> SavePreferencesAsync(IReadOnlyList<PreferenceWrite> items, CancellationToken cancellationToken = default);

    Task<List<PushTokenView>> TokensAsync(CancellationToken cancellationToken = default);

    Task<PushTokenView> RegisterTokenAsync(PushTokenRegister register, CancellationToken cancellationToken = default);

    Task<bool> RemoveTokenAsync(long id, CancellationToken cancellationToken = default);

    Task<List<PushTicketView>> TicketsAsync(CancellationToken cancellationToken = default);

    Task<List<PushTicketView>> SendTestAsync(PushRequest request, CancellationToken cancellationToken = default);
}

public class NotificationService(
    AppDbContext db,
    IHttpContextAccessor http,
    IPushSender sender,
    TimeProvider clock) : INotificationService, IPushNotifier
{
    public const int MaxTokensPerUser = 20;
    public const int HistorySize = 50;

    private static readonly string[] Platforms = ["ios", "android", "web"];

    public async Task<List<PreferenceView>> PreferencesAsync(CancellationToken cancellationToken = default)
    {
        var userId = CallerId();
        var chosen = await db.UserNotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive).ToListAsync(cancellationToken);
        return Matrix(chosen);
    }

    public async Task<List<PreferenceView>> SavePreferencesAsync(IReadOnlyList<PreferenceWrite> items, CancellationToken cancellationToken = default)
    {
        var userId = CallerId();
        var wanted = new Dictionary<(string, string), bool>();
        foreach (var item in items)
        {
            var category = item.Category.Trim().ToUpperInvariant();
            var channel = item.Channel.Trim().ToUpperInvariant();
            if (!NotificationCategories.All.Contains(category))
                throw new BadRequestException($"Unknown category '{item.Category}'. Use: {string.Join(", ", NotificationCategories.All)}");
            if (!NotificationChannels.All.Contains(channel))
                throw new BadRequestException($"Unknown channel '{item.Channel}'. Use: {string.Join(", ", NotificationChannels.All)}");
            if (!item.Enabled && NotificationCategories.Mandatory.Contains(category))
                throw new BadRequestException($"'{category}' notifications cannot be turned off");

            wanted[(category, channel)] = item.Enabled;
        }

        var existing = await db.UserNotificationPreferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        foreach (var ((category, channel), enabled) in wanted)
        {
            var row = existing.FirstOrDefault(p => p.Category == category && p.Channel == channel);
            if (row is null)
                db.UserNotificationPreferences.Add(new UserNotificationPreference
                {
                    UserId = userId, Category = category, Channel = channel, Enabled = enabled,
                });
            else
                row.Enabled = enabled;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await PreferencesAsync(cancellationToken);
    }

    public async Task<List<PushTokenView>> TokensAsync(CancellationToken cancellationToken = default)
    {
        var userId = CallerId();
        return await db.UserPushTokens.AsNoTracking().Where(t => t.UserId == userId && t.IsActive)
            .OrderByDescending(t => t.LastSeenAt).ThenBy(t => t.Id)
            .Select(t => ToView(t)).ToListAsync(cancellationToken);
    }

    public async Task<PushTokenView> RegisterTokenAsync(PushTokenRegister register, CancellationToken cancellationToken = default)
    {
        var userId = CallerId();
        var platform = register.Platform.Trim().ToLowerInvariant();
        if (!Platforms.Contains(platform))
            throw new BadRequestException($"Unknown platform '{register.Platform}'. Use: {string.Join(", ", Platforms)}");

        var token = register.Token.Trim();
        var hash = Hash(token);
        var now = clock.GetUtcNow().UtcDateTime;

        var row = await db.UserPushTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (row is null)
        {
            if (await db.UserPushTokens.CountAsync(t => t.UserId == userId, cancellationToken) >= MaxTokensPerUser)
                throw new BadRequestException($"A person can register at most {MaxTokensPerUser} devices; remove one first");

            row = new UserPushToken { Token = token, TokenHash = hash };
            db.UserPushTokens.Add(row);
        }

        row.UserId = userId;
        row.Platform = platform;
        row.DeviceName = register.DeviceName?.Trim();
        row.LastSeenAt = now;
        row.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        return ToView(row);
    }

    public async Task<bool> RemoveTokenAsync(long id, CancellationToken cancellationToken = default)
    {
        var userId = CallerId();
        var row = await db.UserPushTokens.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);
        if (row is null)
            return false;

        db.UserPushTokens.Remove(row);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<PushTicketView>> TicketsAsync(CancellationToken cancellationToken = default)
    {
        var userId = CallerId();
        return await db.PushTickets.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.Id).Take(HistorySize)
            .Select(t => new PushTicketView
            {
                Id = t.Id, Category = t.Category, Title = t.Title, Body = t.Body, Status = t.Status.ToString(),
                Error = t.Error, SentAt = t.SentAt, CreatedAt = t.CreatedAt,
            }).ToListAsync(cancellationToken);
    }

    public async Task<List<PushTicketView>> SendTestAsync(PushRequest request, CancellationToken cancellationToken = default)
    {
        var category = request.Category.Trim().ToUpperInvariant();
        if (!NotificationCategories.All.Contains(category))
            throw new BadRequestException($"Unknown category '{request.Category}'. Use: {string.Join(", ", NotificationCategories.All)}");

        var tickets = await NotifyAsync(CallerId(), category, request.Title.Trim(), request.Body.Trim(), cancellationToken);
        return tickets.Select(t => new PushTicketView
        {
            Id = t.Id, Category = t.Category, Title = t.Title, Body = t.Body, Status = t.Status.ToString(),
            Error = t.Error, SentAt = t.SentAt, CreatedAt = t.CreatedAt,
        }).ToList();
    }

    public async Task<List<PushTicket>> NotifyAsync(
        long userId, string category, string title, string body, CancellationToken cancellationToken = default)
    {
        category = category.Trim().ToUpperInvariant();
        var tokens = await db.UserPushTokens.Where(t => t.UserId == userId && t.IsActive).OrderBy(t => t.Id).ToListAsync(cancellationToken);
        var tickets = new List<PushTicket>();

        if (!await IsEnabledAsync(userId, category, NotificationChannels.Push, cancellationToken))
        {
            tickets.Add(Ticket(userId, null, category, title, body, PushStatus.Skipped, error: "Turned off by the user"));
        }
        else if (tokens.Count == 0)
        {
            tickets.Add(Ticket(userId, null, category, title, body, PushStatus.Skipped, error: "No registered device"));
        }
        else
        {
            foreach (var token in tokens)
            {
                PushSendResult result;
                try
                {
                    result = await sender.SendAsync(new PushMessage(token.Token, token.Platform, title, body), cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    result = new PushSendResult(false, Error: ex.Message);
                }

                var ticket = Ticket(userId, token.Id, category, title, body,
                    result.Succeeded ? PushStatus.Sent : PushStatus.Failed, result.ProviderTicketId, result.Error);
                ticket.SentAt = result.Succeeded ? clock.GetUtcNow().UtcDateTime : null;
                tickets.Add(ticket);
            }
        }

        db.PushTickets.AddRange(tickets);
        await db.SaveChangesAsync(cancellationToken);
        return tickets;
    }

    private async Task<bool> IsEnabledAsync(long userId, string category, string channel, CancellationToken cancellationToken)
    {
        if (NotificationCategories.Mandatory.Contains(category))
            return true;

        var chosen = await db.UserNotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == userId && p.IsActive && p.Category == category && p.Channel == channel)
            .Select(p => (bool?)p.Enabled).FirstOrDefaultAsync(cancellationToken);
        return chosen ?? NotificationCategories.OnByDefault(category);
    }

    private static List<PreferenceView> Matrix(List<UserNotificationPreference> chosen)
    {
        var result = new List<PreferenceView>();
        foreach (var category in NotificationCategories.All)
            foreach (var channel in NotificationChannels.All)
            {
                var mandatory = NotificationCategories.Mandatory.Contains(category);
                var row = chosen.FirstOrDefault(p => p.Category == category && p.Channel == channel);
                result.Add(new PreferenceView
                {
                    Category = category,
                    Channel = channel,
                    Mandatory = mandatory,
                    IsDefault = row is null,
                    Enabled = mandatory || (row?.Enabled ?? NotificationCategories.OnByDefault(category)),
                });
            }

        return result;
    }

    private static PushTicket Ticket(
        long userId, long? tokenId, string category, string title, string body, PushStatus status, string? providerId = null, string? error = null) => new()
    {
        UserId = userId,
        PushTokenId = tokenId,
        Category = category,
        Title = title,
        Body = body,
        Status = status,
        ProviderTicketId = providerId,
        Error = error is { Length: > 1000 } ? error[..1000] : error,
    };

    private static PushTokenView ToView(UserPushToken t) => new()
    {
        Id = t.Id, Platform = t.Platform, DeviceName = t.DeviceName, LastSeenAt = t.LastSeenAt, CreatedAt = t.CreatedAt,
    };

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private long CallerId() => (http.HttpContext?.User ?? throw new UnauthorizedException()).UserId() ?? throw new UnauthorizedException();
}
