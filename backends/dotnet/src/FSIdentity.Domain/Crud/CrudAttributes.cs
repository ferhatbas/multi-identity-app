namespace FSIdentity.Domain.Crud;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SortableAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SearchableAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class UpperAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class LowerAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MapIgnoreAttribute : Attribute;

[AttributeUsage(AttributeTargets.Property)]
public sealed class MapsFromAttribute(string entityProperty) : Attribute
{
    public string EntityProperty { get; } = entityProperty;
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class UniqueAttribute(string property) : Attribute
{
    public string Property { get; } = property;

    public string? Within { get; init; }

    public bool AcrossScopes { get; init; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ParentAttribute(Type entity) : Attribute
{
    public Type Entity { get; } = entity;

    public string? Column { get; init; }

    public bool CheckOnWrite { get; init; } = true;

    public bool CheckOnRestore { get; init; } = true;

    public bool BlocksDelete { get; init; } = true;

    public bool ChildrenAcrossScopes { get; init; }

    public string? InUseText { get; init; }
}
