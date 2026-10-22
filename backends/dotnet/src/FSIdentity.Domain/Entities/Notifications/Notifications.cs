using FSIdentity.Domain.Audit;

namespace FSIdentity.Domain.Entities.Notifications;

public static class NotificationChannels
{
    public const string Push = "PUSH";
    public const string Email = "EMAIL";

    public static readonly IReadOnlyList<string> All = [Push, Email];
}

public static class NotificationCategories
{
    public const string Security = "SECURITY";
    public const string System = "SYSTEM";
    public const string Activity = "ACTIVITY";
    public const string Marketing = "MARKETING";

    public static readonly IReadOnlyList<string> All = [Security, System, Activity, Marketing];

    public static readonly IReadOnlySet<string> Mandatory = new HashSet<string> { Security };

    public static bool OnByDefault(string category) => category != Marketing;
}

public class UserPushToken : BaseEntityRoot
{
    public long UserId { get; set; }

    [AuditIgnore]
    public string Token { get; set; } = string.Empty;

    [AuditIgnore]
    public string TokenHash { get; set; } = string.Empty;

    public string Platform { get; set; } = string.Empty;

    public string? DeviceName { get; set; }

    public DateTime? LastSeenAt { get; set; }
}

public class UserNotificationPreference : BaseEntityRoot
{
    public long UserId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}

public enum PushStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2,
    Skipped = 3,
}

[NoAudit]
public class PushTicket : BaseEntityRoot
{
    public long UserId { get; set; }

    public long? PushTokenId { get; set; }

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public PushStatus Status { get; set; }

    public string? ProviderTicketId { get; set; }

    public string? Error { get; set; }

    public DateTime? SentAt { get; set; }
}
