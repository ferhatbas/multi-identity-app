namespace FSIdentity.Domain.Events;

public interface IDomainEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent;
}