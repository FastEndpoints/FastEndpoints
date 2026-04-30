using System.Collections.Concurrent;
using System.Reflection;

namespace FastEndpoints.OpenApi;

static class ConstructorDefaultExtensions
{
    static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object?>> _constructorDefaultCache = new();
    static readonly IReadOnlyDictionary<string, object?> _emptyConstructorDefaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    internal static object? GetParentCtorDefaultValue(this PropertyInfo property)
    {
        var parentType = property.DeclaringType;

        if (parentType?.IsClass is not true)
            return null;

        var constructorDefaults = _constructorDefaultCache.GetOrAdd(parentType, CreateConstructorDefaultMap);

        return constructorDefaults.GetValueOrDefault(property.Name);
    }

    static IReadOnlyDictionary<string, object?> CreateConstructorDefaultMap(Type type)
    {
        var parameters = type.GetConstructors()
                             .MaxBy(static c => c.GetParameters().Length)
                             ?.GetParameters();

        if (parameters is null)
            return _emptyConstructorDefaults;

        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters)
        {
            if (parameter.HasDefaultValue && parameter.Name is not null)
                defaults[parameter.Name] = parameter.DefaultValue;
        }

        return defaults.Count == 0 ? _emptyConstructorDefaults : defaults;
    }
}