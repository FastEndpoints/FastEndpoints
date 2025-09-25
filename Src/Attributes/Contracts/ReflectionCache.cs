using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

#pragma warning disable CS1591

/// <summary>
/// the central repository of reflection related data for request dtos and their children
/// </summary>
public sealed class ReflectionCache : ConcurrentDictionary<Type, TypeDefinition>;

/// <summary>
/// represents reflection data for a given type
/// </summary>
public sealed class TypeDefinition
{
    /// <summary>
    /// a func for creating a new blank instance of a type
    /// </summary>
    public Func<object>? ObjectFactory { get; set; }

    /// <summary>
    /// the reflection data for all the properties of a type
    /// </summary>
    public ConcurrentDictionary<PropertyInfo, PropertyDefinition>? Properties { get; set; }

    /// <summary>
    /// a func used for converting string values to the respective type by calling it's <c>TryParse()</c> method.
    /// </summary>
    public Func<StringValues, ParseResult>? ValueParser { get; set; }

    /// <summary>
    /// indicates if this type, or it's immediate properties has data annotation validation attributes.
    /// </summary>
    public bool? IsValidatable { get; set; }
}

/// <summary>
/// represents reflection data for a property of a type
/// </summary>
public sealed class PropertyDefinition
{
    /// <summary>
    /// action used for setting the value of a property on a class
    /// </summary>
    public Action<object, object?>? Setter { get; set; }
}