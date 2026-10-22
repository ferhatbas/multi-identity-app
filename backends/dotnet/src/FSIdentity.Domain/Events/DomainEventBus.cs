using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FSIdentity.Domain.Events;

public sealed class DomainEventBus(IServiceScopeFactory scopeFactory, ILogger<DomainEventBus> logger)
    : IDomainEventBus
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        using var scope = scopeFactory.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IDomainEventHandler<TEvent>>().ToList();

        if (handlers.Count == 0)
        {
            logger.LogDebug("No handlers for {Event}", typeof(TEvent).Name);
            return;
        }

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Handler {Handler} failed for {Event}",
                    handler.GetType().Name, typeof(TEvent).Name);
            }
        }
    }
}