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
    /// materialized snapshot of <see cref="Properties"/>' keys, cached so the bindable-props hot path (complex binders +
    /// data-annotation validation recursion) doesn't allocate a fresh <c>List&lt;PropertyInfo&gt;</c> from
    /// <see cref="ConcurrentDictionary{TKey,TValue}.Keys"/> on every call. assumes no post-startup key additions to
    /// <see cref="Properties"/> (which the public api does not do).
    /// </summary>
    internal PropertyInfo[]? BindableProps { get; set; }

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
    /// func used for getting the value of a property from a class
    /// </summary>
    public Func<object, object?>? Getter { get; set; }

    /// <summary>
    /// action used for setting the value of a property on a class
    /// </summary>
    public Action<object, object?>? Setter { get; set; }

    /// <summary>
    /// the keyed-service key when this property is decorated with <see cref="KeyedServiceAttribute"/>, otherwise null.
    /// </summary>
    public string? ServiceKey { get; set; }

    /// <summary>
    /// field name used for request binding (from <c>BindFrom</c> or the configured naming policy).
    /// populated lazily on first complex form/query bind; non-null means complex-bind metadata is initialized.
    /// </summary>
    internal string? FieldName { get; set; }

    /// <summary>
    /// underlying (nullable-stripped) property type for complex binding.
    /// </summary>
    internal Type? UnderlyingType { get; set; }

    /// <summary>
    /// whether the underlying property type is a complex (non-simple) type.
    /// </summary>
    internal bool IsComplex { get; set; }

    /// <summary>
    /// whether the underlying property type is a collection (excluding <see cref="string"/>).
    /// </summary>
    internal bool IsCollection { get; set; }

    /// <summary>
    /// whether the underlying property type is an <c>IFormFile</c>.
    /// </summary>
    internal bool IsFormFile { get; set; }

    /// <summary>
    /// whether the underlying property type is an <c>IEnumerable&lt;IFormFile&gt;</c>.
    /// </summary>
    internal bool IsFormFileCollection { get; set; }

    /// <summary>
    /// element type when <see cref="IsCollection"/> is true; otherwise null.
    /// </summary>
    internal Type? ElementType { get; set; }

    /// <summary>
    /// whether collection elements are complex types (only meaningful when <see cref="IsCollection"/>).
    /// </summary>
    internal bool ElementIsComplex { get; set; }

    /// <summary>
    /// factory that creates a <c>List&lt;TElement&gt;</c> for collection binding.
    /// </summary>
    internal Func<object>? ListFactory { get; set; }

    /// <summary>
    /// value parser for simple property types, or simple collection element types.
    /// </summary>
    internal Func<StringValues, ParseResult>? ValueParser { get; set; }
}
