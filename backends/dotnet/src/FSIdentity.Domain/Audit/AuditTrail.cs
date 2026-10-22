using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using FSIdentity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FSIdentity.Domain.Audit;

internal sealed record PendingAudit(
    string EntityName, string? EntityId, string Action,
    string? OldValues, string? NewValues, string? ChangedFields,
    EntityEntry? Added);

internal static class AuditTrail
{
    private static readonly HashSet<string> Stamps = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(IAuditable.CreatedAt), nameof(IAuditable.CreatedBy),
        nameof(IAuditable.LastModifiedAt), nameof(IAuditable.LastModifiedBy),
        nameof(IAuditable.DeletedAt), nameof(IAuditable.DeletedBy),
    };

    private static readonly HashSet<string> Secrets = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "Password", "TwoFactorSecret", "CodeHash", "TokenHash",
    };

    private static readonly ConcurrentDictionary<Type, bool> OptedOut = new();
    private static readonly ConcurrentDictionary<IProperty, bool> Hidden = new();

    public static List<PendingAudit> Collect(ChangeTracker tracker)
    {
        var pending = new List<PendingAudit>();

        if (AuditLogSuppression.IsSuppressed)
            return pending;

        foreach (var entry in tracker.Entries())
        {
            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            var type = entry.Entity.GetType();
            if (type == typeof(AuditLog)
                || OptedOut.GetOrAdd(type, t => t.GetCustomAttribute<NoAuditAttribute>(inherit: true) is not null))
                continue;

            var audit = entry.State switch
            {
                EntityState.Added => Created(entry),
                EntityState.Deleted => Removed(entry),
                _ => Changed(entry),
            };

            if (audit is not null)
                pending.Add(audit);
        }

        return pending;
    }

    public static AuditLog ToRow(PendingAudit pending) => new()
    {
        EntityName = pending.EntityName,
        EntityId = pending.Added is not null ? KeyOf(pending.Added) : pending.EntityId,
        Action = pending.Action,
        OldValues = pending.OldValues,
        NewValues = pending.NewValues,
        ChangedFields = pending.ChangedFields,
    };

    public static AuditLog NoteRow(AuditNote.Entry note, int rows) => new()
    {
        EntityName = note.Subject,
        EntityId = note.SubjectId,
        Action = "Publish",
        NewValues = JsonSerializer.Serialize(new { note = note.Note, rows }),
    };

    private static PendingAudit Created(EntityEntry entry)
    {
        var values = Snapshot(entry, current: true);
        return new PendingAudit(entry.Entity.GetType().Name, null, "Create", null, Json(values), null, entry);
    }

    private static PendingAudit Removed(EntityEntry entry)
        => new(entry.Entity.GetType().Name, KeyOf(entry), "Delete",
            Json(Snapshot(entry, current: false)), null, null, null);

    private static PendingAudit? Changed(EntityEntry entry)
    {
        var before = new Dictionary<string, object?>();
        var after = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            if (!property.IsModified || Stamps.Contains(name) || IsHidden(property.Metadata))
                continue;
            if (Equals(property.OriginalValue, property.CurrentValue))
                continue;

            before[name] = property.OriginalValue;
            after[name] = property.CurrentValue;
        }

        if (before.Count == 0)
            return null;

        var action = "Update";
        if (before.TryGetValue(nameof(ISoftDelete.IsDeleted), out var was)
            && after[nameof(ISoftDelete.IsDeleted)] is bool now)
            action = was is false && now ? "Delete" : "Restore";

        return new PendingAudit(
            entry.Entity.GetType().Name, KeyOf(entry), action,
            Json(before), Json(after), string.Join(",", before.Keys), null);
    }

    private static Dictionary<string, object?> Snapshot(EntityEntry entry, bool current)
        => entry.Properties
            .Where(p => !IsHidden(p.Metadata))
            .ToDictionary(p => p.Metadata.Name, p => current ? p.CurrentValue : p.OriginalValue);

    private static string? KeyOf(EntityEntry entry)
        => entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString();

    private static bool IsHidden(IProperty property)
        => Hidden.GetOrAdd(property, p =>
            Secrets.Contains(p.Name) || p.PropertyInfo?.GetCustomAttribute<AuditIgnoreAttribute>() is not null);

    private static string Json(Dictionary<string, object?> values) => JsonSerializer.Serialize(values);
}
