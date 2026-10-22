using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using FSIdentity.Domain.Data;
using FSIdentity.Domain.Exceptions;
using FSIdentity.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FSIdentity.Domain.Crud;

internal static class CrudIntegrity<TEntity> where TEntity : class, IBaseEntityRoot
{
    private sealed record UniqueRule(
        PropertyInfo Column, Func<TEntity, object?> Value,
        (PropertyInfo Column, Func<TEntity, object?> Value)[] Within, bool AcrossScopes);

    private sealed record ParentRule(
        PropertyInfo Column, Func<TEntity, long?> Value, ParentAttribute Attribute, IProbe Parent);

    private sealed record ChildRule(Type Child, string Column, ParentAttribute Attribute, IProbe Probe);

    private static readonly string Label = CrudText.Label(typeof(TEntity));

    private static readonly IReadOnlyList<UniqueRule> Uniques = typeof(TEntity)
        .GetCustomAttributes<UniqueAttribute>()
        .Select(a => new UniqueRule(
            Column(a.Property), Getter(Column(a.Property)),
            (a.Within ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(name => (Column: Column(name), Value: Getter(Column(name))))
                .ToArray(),
            a.AcrossScopes))
        .ToList();

    private static readonly IReadOnlyList<ParentRule> Parents = ParentLinks(typeof(TEntity))
        .Select(x => new ParentRule(x.Column, ForeignKey(x.Column), x.Attribute, Probes.For(x.Attribute.Entity)))
        .ToList();

    private static readonly Lazy<IReadOnlyList<ChildRule>> Children = new(() => typeof(TEntity).Assembly
        .GetTypes()
        .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IBaseEntityRoot).IsAssignableFrom(t))
        .SelectMany(t => ParentLinks(t)
            .Where(x => x.Attribute is { BlocksDelete: true } && x.Attribute.Entity == typeof(TEntity))
            .Select(x => new ChildRule(t, x.Column.Name, x.Attribute, Probes.For(t))))
        .ToList());

    public static void Validate()
    {
        _ = Uniques;
        _ = Parents;
        _ = Children.Value;
    }

    public static async Task EnsureUniqueAsync(IUnitOfWork uow, TEntity entity, CancellationToken cancellationToken)
    {
        foreach (var rule in Uniques)
        {
            var value = rule.Value(entity);
            var e = Expression.Parameter(typeof(TEntity), "e");

            var filter = Expression.AndAlso(
                Expression.Equal(Expression.Property(e, rule.Column), Parameter(value, rule.Column.PropertyType)),
                Expression.NotEqual(Expression.Property(e, nameof(IBaseEntityRoot.Id)), Parameter(entity.Id, typeof(long))));

            foreach (var (column, read) in rule.Within)
                filter = Expression.AndAlso(filter, Expression.Equal(
                    Expression.Property(e, column), Parameter(read(entity), column.PropertyType)));

            var repository = uow.Repository<TEntity>();
            var taken = await (rule.AcrossScopes ? repository.QueryAcrossScopes() : repository.Query())
                .AnyAsync(Expression.Lambda<Func<TEntity, bool>>(filter, e), cancellationToken);

            if (taken)
                throw new ConflictException(
                    $"{Label} {CrudText.PropertyWords(rule.Column.Name)} '{value}' already exists");
        }
    }

    public static async Task EnsureParentsExistAsync(IUnitOfWork uow, TEntity entity, CancellationToken cancellationToken)
    {
        foreach (var rule in Parents.Where(r => r.Attribute.CheckOnWrite))
        {
            if (rule.Value(entity) is { } id && !await rule.Parent.ExistsAsync(uow, id, cancellationToken))
                throw new BadRequestException($"{CrudText.Label(rule.Attribute.Entity)} {id} does not exist");
        }
    }

    public static async Task EnsureParentsAreLiveAsync(IUnitOfWork uow, TEntity entity, CancellationToken cancellationToken)
    {
        foreach (var rule in Parents.Where(r => r.Attribute.CheckOnRestore))
        {
            if (rule.Value(entity) is { } id && !await rule.Parent.ExistsAsync(uow, id, cancellationToken))
                throw new ConflictException(
                    $"{CrudText.Label(rule.Attribute.Entity)} {id} is deleted; restore it first");
        }
    }

    public static async Task EnsureNoChildrenAsync(IUnitOfWork uow, TEntity entity, CancellationToken cancellationToken)
    {
        foreach (var rule in Children.Value)
        {
            if (!await rule.Probe.AnyPointingAtAsync(
                    uow, rule.Column, entity.Id, rule.Attribute.ChildrenAcrossScopes, cancellationToken))
                continue;

            var text = rule.Attribute.InUseText ?? $"still has {CrudText.PluralWords(rule.Child)}";
            throw new ConflictException($"{Label} '{NameOf(entity)}' {text} and cannot be deleted");
        }
    }

    private static IEnumerable<(PropertyInfo Column, ParentAttribute Attribute)> ParentLinks(Type entity)
    {
        foreach (var property in entity.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            foreach (var attribute in property.GetCustomAttributes<ParentAttribute>())
                yield return (property, attribute);

        foreach (var attribute in entity.GetCustomAttributes<ParentAttribute>().Where(a => a.Column is not null))
            yield return (entity.GetProperty(attribute.Column!, BindingFlags.Public | BindingFlags.Instance)
                          ?? throw new InvalidOperationException($"{entity.Name} has no property '{attribute.Column}'"),
                attribute);
    }

    private static string NameOf(TEntity entity)
        => (typeof(TEntity).GetProperty("Code") ?? typeof(TEntity).GetProperty("Name"))?.GetValue(entity)?.ToString()
           ?? entity.Id.ToString();

    private static PropertyInfo Column(string name)
        => typeof(TEntity).GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
           ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no property '{name}'");

    private static Func<TEntity, object?> Getter(PropertyInfo property)
    {
        var e = Expression.Parameter(typeof(TEntity), "e");
        return Expression.Lambda<Func<TEntity, object?>>(
            Expression.Convert(Expression.Property(e, property), typeof(object)), e).Compile();
    }

    private static Func<TEntity, long?> ForeignKey(PropertyInfo property)
    {
        if (property.PropertyType != typeof(long) && property.PropertyType != typeof(long?))
            throw new InvalidOperationException(
                $"{typeof(TEntity).Name}.{property.Name} is [Parent] so it must be a long or long?");

        var e = Expression.Parameter(typeof(TEntity), "e");
        return Expression.Lambda<Func<TEntity, long?>>(
            Expression.Convert(Expression.Property(e, property), typeof(long?)), e).Compile();
    }

    private static Expression Parameter(object? value, Type type)
        => Expression.Field(
            Expression.Constant(Activator.CreateInstance(typeof(Box<>).MakeGenericType(type), value)),
            nameof(Box<int>.Value));
}

internal interface IProbe
{
    Task<bool> ExistsAsync(IUnitOfWork uow, long id, CancellationToken cancellationToken);

    Task<bool> AnyPointingAtAsync(
        IUnitOfWork uow, string foreignKey, long id, bool acrossScopes, CancellationToken cancellationToken);
}

internal static class Probes
{
    private static readonly ConcurrentDictionary<Type, IProbe> Cache = new();

    public static IProbe For(Type entity) => Cache.GetOrAdd(entity,
        type => (IProbe)Activator.CreateInstance(typeof(Probe<>).MakeGenericType(type))!);

    private sealed class Probe<T> : IProbe where T : class, IBaseEntityRoot
    {
        public Task<bool> ExistsAsync(IUnitOfWork uow, long id, CancellationToken cancellationToken)
            => uow.Repository<T>().Query().AnyAsync(e => e.Id == id, cancellationToken);

        public Task<bool> AnyPointingAtAsync(
            IUnitOfWork uow, string foreignKey, long id, bool acrossScopes, CancellationToken cancellationToken)
        {
            var repository = uow.Repository<T>();
            var rows = acrossScopes ? repository.QueryAcrossScopes() : repository.Query();

            var e = Expression.Parameter(typeof(T), "e");
            var column = Expression.Convert(Expression.Property(e, foreignKey), typeof(long?));
            var wanted = Expression.Convert(
                Expression.Field(Expression.Constant(new Box<long>(id)), nameof(Box<long>.Value)), typeof(long?));

            return rows.AnyAsync(
                Expression.Lambda<Func<T, bool>>(Expression.Equal(column, wanted), e), cancellationToken);
        }
    }
}
