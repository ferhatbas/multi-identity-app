using System.Reflection;
using System.Runtime.ExceptionServices;
using FSIdentity.Domain.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FSIdentity.Domain.Crud;

public static class CrudServiceCollection
{
    public static IServiceCollection AddCrud(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.TryAddScoped(typeof(ICrudService<,,,>), typeof(CrudService<,,,>));

        var types = assemblies.SelectMany(a => a.GetTypes()).Where(t => t is { IsClass: true, IsAbstract: false });
        foreach (var type in types)
        {
            foreach (var rules in type.GetInterfaces().Where(i => IsGeneric(i, typeof(ICrudRules<,,>)) || IsGeneric(i, typeof(IPostCreateHook<>))))
                services.AddScoped(rules, type);

            if (CrudControllerArguments(type) is { } arguments)
            {
                Invoke(typeof(CrudMap<,,,>), arguments, "Validate");
                Invoke(typeof(CrudIntegrity<>), [arguments[0]], "Validate");
            }
        }

        return services;
    }

    private static bool IsGeneric(Type type, Type definition)
        => type.IsGenericType && type.GetGenericTypeDefinition() == definition;

    private static Type[]? CrudControllerArguments(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(CrudController<,,,>))
                return t.GetGenericArguments();
        return null;
    }

    private static void Invoke(Type definition, Type[] arguments, string method)
    {
        try
        {
            definition.MakeGenericType(arguments)
                .GetMethod(method, BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
        }
    }
}
