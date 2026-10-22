using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace FSIdentity.Domain.Events;

public static class DomainEventServiceCollection
{
    public static IServiceCollection AddDomainEvents(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddSingleton<IDomainEventBus, DomainEventBus>();

        var handlers = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in handlers)
            foreach (var contract in type.GetInterfaces().Where(i =>
                         i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)))
                services.AddScoped(contract, type);

        return services;
    }
}