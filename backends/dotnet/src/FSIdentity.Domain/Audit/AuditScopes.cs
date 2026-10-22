namespace FSIdentity.Domain.Audit;

public static class AuditLogSuppression
{
    private static readonly AsyncLocal<bool> Flag = new();

    public static bool IsSuppressed => Flag.Value;

    public static IDisposable Suppress()
    {
        var previous = Flag.Value;
        Flag.Value = true;
        return new Restore(() => Flag.Value = previous);
    }

    internal sealed class Restore(Action restore) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            restore();
        }
    }
}

public static class AuditNote
{
    public sealed record Entry(string Subject, string? SubjectId, string Note);

    private static readonly AsyncLocal<Entry?> Current = new();

    public static Entry? Value => Current.Value;

    public static IDisposable Set(string subject, string? subjectId, string note)
    {
        var previous = Current.Value;
        Current.Value = new Entry(subject, subjectId, note);
        return new AuditLogSuppression.Restore(() => Current.Value = previous);
    }
}
