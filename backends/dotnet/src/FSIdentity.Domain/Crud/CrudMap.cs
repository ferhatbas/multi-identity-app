using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using FSIdentity.Domain.Interfaces;

namespace FSIdentity.Domain.Crud;

internal sealed class CrudMap<TEntity, TRead, TCreate, TUpdate>
    where TEntity : class, IBaseEntityRoot, new()
{
    private static readonly Lazy<CrudMap<TEntity, TRead, TCreate, TUpdate>> Cached = new(() => new());

    public static CrudMap<TEntity, TRead, TCreate, TUpdate> Instance => Cached.Value;

    private static readonly HashSet<string> ServerOwned =
    [
        nameof(IBaseEntityRoot.Id),
        nameof(IAuditable.CreatedBy), nameof(IAuditable.CreatedAt),
        nameof(IAuditable.LastModifiedBy), nameof(IAuditable.LastModifiedAt),
        nameof(IAuditable.DeletedBy), nameof(IAuditable.DeletedAt), nameof(ISoftDelete.IsDeleted),
        "IdentityTenantId", "IdentityCompanyId",
    ];

    public Expression<Func<TEntity, TRead>> ReadExpression { get; }

    public Func<TEntity, TRead> ToRead { get; }

    public Func<TCreate, TEntity> ToEntity { get; }

    public Action<TUpdate, TEntity> Apply { get; }

    public IReadOnlyDictionary<string, Expression<Func<TEntity, object?>>> Sortable { get; }

    public IReadOnlyList<PropertyInfo> SearchColumns { get; }

    private CrudMap()
    {
        ReadExpression = BuildRead();
        ToRead = ReadExpression.Compile();
        ToEntity = BuildToEntity();
        Apply = BuildApply();
        Sortable = BuildSortable();
        SearchColumns = BuildSearchColumns();
    }

    public static void Validate() => _ = Instance;

    public Expression<Func<TEntity, bool>>? BuildSearch(string term)
    {
        if (SearchColumns.Count == 0)
            return null;

        var e = Expression.Parameter(typeof(TEntity), "e");
        var termValue = Expression.Field(Expression.Constant(new Box<string>(term)), nameof(Box<string>.Value));
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        var body = SearchColumns
            .Select(column => (Expression)Expression.Call(Expression.Property(e, column), contains, termValue))
            .Aggregate(Expression.OrElse);
        return Expression.Lambda<Func<TEntity, bool>>(body, e);
    }

    private static Expression<Func<TEntity, TRead>> BuildRead()
    {
        var e = Expression.Parameter(typeof(TEntity), "e");
        var sources = new Dictionary<string, Expression>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in typeof(TRead).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            sources[property.Name] = SourceFor(property, e);

        var parameterless = typeof(TRead).GetConstructor(Type.EmptyTypes);
        Expression create;
        if (parameterless is not null)
        {
            var bindings = typeof(TRead).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod?.IsPublic == true)
                .Select(p => Expression.Bind(p, sources[p.Name]));
            create = Expression.MemberInit(Expression.New(parameterless), bindings);
        }
        else
        {
            var constructor = typeof(TRead).GetConstructors().OrderByDescending(c => c.GetParameters().Length).FirstOrDefault()
                ?? throw new InvalidOperationException($"{typeof(TRead).Name} has no public constructor");
            var arguments = constructor.GetParameters().Select(p =>
                sources.TryGetValue(p.Name!, out var source)
                    ? source
                    : throw new InvalidOperationException(
                        $"{typeof(TRead).Name}: constructor parameter '{p.Name}' has no matching property"));
            create = Expression.New(constructor, arguments);
        }

        return Expression.Lambda<Func<TEntity, TRead>>(create, e);
    }

    private static Expression SourceFor(PropertyInfo readProperty, ParameterExpression e)
    {
        var sourceName = readProperty.GetCustomAttribute<MapsFromAttribute>()?.EntityProperty ?? readProperty.Name;
        var source = typeof(TEntity).GetProperty(sourceName, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException(
                $"{typeof(TRead).Name}.{readProperty.Name} has no source: {typeof(TEntity).Name} has no property " +
                $"'{sourceName}'. Name it the same or add [MapsFrom].");

        return Coerce(Expression.Property(e, source), readProperty.PropertyType,
            $"{typeof(TRead).Name}.{readProperty.Name}");
    }

    private static Func<TCreate, TEntity> BuildToEntity()
    {
        var dto = Expression.Parameter(typeof(TCreate), "create");
        var entity = Expression.Variable(typeof(TEntity), "entity");
        var steps = new List<Expression> { Expression.Assign(entity, Expression.New(typeof(TEntity))) };
        steps.AddRange(Assignments<TCreate>(dto, entity));
        steps.Add(entity);
        return Expression.Lambda<Func<TCreate, TEntity>>(Expression.Block([entity], steps), dto).Compile();
    }

    private static Action<TUpdate, TEntity> BuildApply()
    {
        var dto = Expression.Parameter(typeof(TUpdate), "update");
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var steps = Assignments<TUpdate>(dto, entity).ToList();
        steps.Add(Expression.Empty());
        return Expression.Lambda<Action<TUpdate, TEntity>>(Expression.Block(steps), dto, entity).Compile();
    }

    private static IEnumerable<Expression> Assignments<TDto>(ParameterExpression dto, Expression entity)
    {
        foreach (var property in typeof(TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0
                || property.GetCustomAttribute<MapIgnoreAttribute>() is not null)
                continue;

            var where = $"{typeof(TDto).Name}.{property.Name}";
            var target = typeof(TEntity).GetProperty(property.Name, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"{where} has no column: {typeof(TEntity).Name} has no property '{property.Name}'. " +
                    "Rename it, or mark it [MapIgnore] if a rule handles it.");

            if (ServerOwned.Contains(target.Name))
                throw new InvalidOperationException(
                    $"{where} would write {typeof(TEntity).Name}.{target.Name}, which only the server sets");
            if (target.SetMethod?.IsPublic != true)
                throw new InvalidOperationException($"{typeof(TEntity).Name}.{target.Name} cannot be written");

            Expression value = Expression.Property(dto, property);
            var targetMember = Expression.Property(entity, target);

            if (Nullable.GetUnderlyingType(value.Type) is { } inner && inner == target.PropertyType)
            {
                yield return Expression.IfThen(
                    Expression.Property(value, nameof(Nullable<int>.HasValue)),
                    Expression.Assign(targetMember, Expression.Property(value, nameof(Nullable<int>.Value))));
                continue;
            }

            if (value.Type == typeof(string))
                value = Expression.Call(typeof(CrudText).GetMethod(nameof(CrudText.Normalize))!,
                    value, Expression.Constant(CaseOf(property)));

            yield return Expression.Assign(targetMember, Coerce(value, target.PropertyType, where));
        }
    }

    private static TextCase CaseOf(PropertyInfo property)
        => property.GetCustomAttribute<UpperAttribute>() is not null ? TextCase.Upper
            : property.GetCustomAttribute<LowerAttribute>() is not null ? TextCase.Lower
            : TextCase.Keep;

    private static IReadOnlyDictionary<string, Expression<Func<TEntity, object?>>> BuildSortable()
    {
        var map = new Dictionary<string, Expression<Func<TEntity, object?>>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in typeof(TRead).GetProperties().Where(p => p.GetCustomAttribute<SortableAttribute>() is not null))
        {
            var e = Expression.Parameter(typeof(TEntity), "e");
            var column = Expression.Property(e, EntityPropertyFor(property));
            map[JsonNamingPolicy.CamelCase.ConvertName(property.Name)] =
                Expression.Lambda<Func<TEntity, object?>>(Expression.Convert(column, typeof(object)), e);
        }
        return map;
    }

    private static IReadOnlyList<PropertyInfo> BuildSearchColumns()
    {
        var columns = typeof(TRead).GetProperties()
            .Where(p => p.GetCustomAttribute<SearchableAttribute>() is not null)
            .Select(EntityPropertyFor)
            .ToList();

        foreach (var column in columns.Where(c => c.PropertyType != typeof(string)))
            throw new InvalidOperationException($"{typeof(TEntity).Name}.{column.Name} is [Searchable] but is not text");
        return columns;
    }

    private static PropertyInfo EntityPropertyFor(PropertyInfo readProperty)
        => typeof(TEntity).GetProperty(
               readProperty.GetCustomAttribute<MapsFromAttribute>()?.EntityProperty ?? readProperty.Name)
           ?? throw new InvalidOperationException($"{typeof(TRead).Name}.{readProperty.Name} has no source property");

    private static Expression Coerce(Expression value, Type target, string where)
    {
        if (value.Type == target)
            return value;
        if (Nullable.GetUnderlyingType(target) == value.Type)
            return Expression.Convert(value, target);
        throw new InvalidOperationException($"{where} is {target.Name} but its source is {value.Type.Name}");
    }
}

internal sealed class Box<T>(T value)
{
    public T Value = value;
}
