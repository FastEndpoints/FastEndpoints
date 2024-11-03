using System.Collections.Concurrent;
using System.Reflection;

namespace FastEndpoints;

#pragma warning disable CS1591

/// <summary>
/// the central repository of reflection related data for request dtos and their children
/// </summary>
public sealed class ReflectionCache : ConcurrentDictionary<Type, ClassDefinition>;

/// <summary>
/// represents reflection data for a given class
/// </summary>
public sealed class ClassDefinition
{
    /// <summary>
    /// a func for creating a new blank instance of a type
    /// </summary>
    public Func<object>? ObjectFactory { get; set; }

    /// <summary>
    /// the reflection data for all the properties of a type
    /// </summary>
    public ConcurrentDictionary<PropertyInfo, PropertyDefinition>? Properties { get; set; }
}

/// <summary>
/// represents reflection data for a property of a class
/// </summary>
public sealed class PropertyDefinition
{
    /// <summary>
    /// action used for setting the value of a property on a class
    /// </summary>
    public Action<object, object?>? Setter { get; set; }
}