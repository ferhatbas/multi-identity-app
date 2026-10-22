namespace FSIdentity.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}