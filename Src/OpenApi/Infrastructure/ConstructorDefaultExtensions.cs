using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FastEndpoints.OpenApi;

static class ConstructorDefaultExtensions
{
    static readonly ConcurrentDictionary<Type, FrozenDictionary<string, object?>> _constructorDefaultCache = new();
    static readonly FrozenDictionary<string, object?> _emptyConstructorDefaults = FrozenDictionary<string, object?>.Empty;

    internal static object? GetParentCtorDefaultValue(this PropertyInfo property)
    {
        var parentType = property.DeclaringType;

        if (parentType?.IsClass is not true)
            return null;

        var constructorDefaults = _constructorDefaultCache.GetOrAdd(parentType, CreateConstructorDefaultMap);

        return constructorDefaults.GetValueOrDefault(property.Name);
    }

    [UnconditionalSuppressMessage("aot", "IL2070")]
    static FrozenDictionary<string, object?> CreateConstructorDefaultMap(Type type)
    {
        var parameters = type.GetConstructors()
                             .MaxBy(static c => c.GetParameters().Length)?
                             .GetParameters()
                             .Where(p => p is { HasDefaultValue: true, Name: not null });

        if (parameters is null || !parameters.Any())
            return _emptyConstructorDefaults;

        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters)
            defaults[parameter.Name!] = parameter.DefaultValue;

        return defaults.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}