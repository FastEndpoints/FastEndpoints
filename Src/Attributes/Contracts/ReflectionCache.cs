using System.Collections.Concurrent;
using System.Reflection;

namespace FastEndpoints;

#pragma warning disable CS1591

public sealed class ReflectionCache : ConcurrentDictionary<Type, ClassDefinition>;

public sealed class ClassDefinition
{
    public Func<object>? ObjectFactory { get; set; }
    public ConcurrentDictionary<PropertyInfo, PropertyDefinition>? Properties { get; set; }
}

public sealed class PropertyDefinition
{
    public Action<object, object?>? Setter { get; set; }
}