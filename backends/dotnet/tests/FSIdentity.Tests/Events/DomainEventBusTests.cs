using FSIdentity.Domain.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FSIdentity.Tests.Events;

public class DomainEventBusTests
{
    public record Happened(string What) : IDomainEvent
    {
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }

    public class Journal
    {
        public List<string> Lines { get; } = [];
    }

    public class First(Journal journal) : IDomainEventHandler<Happened>
    {
        public Task HandleAsync(Happened @event, CancellationToken cancellationToken = default)
        {
            journal.Lines.Add($"first:{@event.What}");
            return Task.CompletedTask;
        }
    }

    public class Broken : IDomainEventHandler<Happened>
    {
        public Task HandleAsync(Happened @event, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }

    public class Last(Journal journal) : IDomainEventHandler<Happened>
    {
        public Task HandleAsync(Happened @event, CancellationToken cancellationToken = default)
        {
            journal.Lines.Add($"last:{@event.What}");
            return Task.CompletedTask;
        }
    }

    public class ScopeMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public class SeesScope(Journal journal, ScopeMarker marker) : IDomainEventHandler<Happened>
    {
        public Task HandleAsync(Happened @event, CancellationToken cancellationToken = default)
        {
            journal.Lines.Add(marker.Id.ToString());
            return Task.CompletedTask;
        }
    }

    public class AlsoSeesScope(Journal journal, ScopeMarker marker) : IDomainEventHandler<Happened>
    {
        public Task HandleAsync(Happened @event, CancellationToken cancellationToken = default)
        {
            journal.Lines.Add(marker.Id.ToString());
            return Task.CompletedTask;
        }
    }

    public class WaitsForCancel : IDomainEventHandler<Happened>
    {
        public Task HandleAsync(Happened @event, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class ErrorLog : ILogger<DomainEventBus>
    {
        public List<string> Errors { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error)
                Errors.Add(formatter(state, exception));
        }
    }

    private static (IDomainEventBus Bus, Journal Journal, ErrorLog Log) Build(params Type[] handlers)
    {
        var journal = new Journal();
        var log = new ErrorLog();
        var services = new ServiceCollection();
        services.AddSingleton(journal);
        services.AddScoped<ScopeMarker>();
        services.AddSingleton<ILogger<DomainEventBus>>(log);
        services.AddSingleton<IDomainEventBus, DomainEventBus>();
        foreach (var handler in handlers)
            services.AddScoped(typeof(IDomainEventHandler<Happened>), handler);

        return (services.BuildServiceProvider().GetRequiredService<IDomainEventBus>(), journal, log);
    }

    [Fact]
    public async Task Every_handler_of_the_event_runs()
    {
        var (bus, journal, _) = Build(typeof(First), typeof(Last));

        await bus.PublishAsync(new Happened("x"));

        Assert.Equal(["first:x", "last:x"], journal.Lines);
    }

    [Fact]
    public async Task Publishing_with_no_handler_is_not_an_error()
    {
        var (bus, journal, log) = Build();

        await bus.PublishAsync(new Happened("x"));

        Assert.Empty(journal.Lines);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task A_failing_handler_is_logged_and_the_others_still_run()
    {
        var (bus, journal, log) = Build(typeof(First), typeof(Broken), typeof(Last));

        await bus.PublishAsync(new Happened("x"));

        Assert.Equal(["first:x", "last:x"], journal.Lines);
        var error = Assert.Single(log.Errors);
        Assert.Contains("Broken", error);
        Assert.Contains("Happened", error);
    }

    [Fact]
    public async Task Handlers_of_one_publication_share_a_scope_and_each_publication_gets_its_own()
    {
        var (bus, journal, _) = Build(typeof(SeesScope), typeof(AlsoSeesScope));

        await bus.PublishAsync(new Happened("a"));
        await bus.PublishAsync(new Happened("b"));

        Assert.Equal(4, journal.Lines.Count);
        Assert.Equal(journal.Lines[0], journal.Lines[1]);
        Assert.Equal(journal.Lines[2], journal.Lines[3]);
        Assert.NotEqual(journal.Lines[0], journal.Lines[2]);
    }

    [Fact]
    public async Task Cancellation_reaches_the_publisher()
    {
        var (bus, _, log) = Build(typeof(WaitsForCancel));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => bus.PublishAsync(new Happened("x"), source.Token));
        Assert.Empty(log.Errors);
    }

    [Fact]
    public async Task A_null_event_is_refused()
    {
        var (bus, _, _) = Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => bus.PublishAsync<Happened>(null!));
    }

    [Fact]
    public void AddDomainEvents_finds_handlers_by_scanning_an_assembly()
    {
        var services = new ServiceCollection();

        services.AddDomainEvents(typeof(DomainEventBusTests).Assembly);

        var registered = services
            .Where(s => s.ServiceType == typeof(IDomainEventHandler<Happened>))
            .Select(s => s.ImplementationType)
            .ToHashSet();
        Assert.Contains(typeof(First), registered);
        Assert.Contains(typeof(Broken), registered);
        Assert.Contains(services, s => s.ServiceType == typeof(IDomainEventBus));
    }
}