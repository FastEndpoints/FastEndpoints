using System.Collections.Concurrent;
using System.Reflection;

namespace FastEndpoints.OpenApi;

static class ConstructorDefaultExtensions
{
    static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, object?>> _constructorDefaultCache = new();

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
                             .Select(c => c.GetParameters())
                             .MaxBy(p => p.Length);

        if (parameters is null)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var defaults = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in parameters)
        {
            if (parameter.HasDefaultValue && parameter.Name is not null)
                defaults[parameter.Name] = parameter.DefaultValue;
        }

        return defaults;
    }
}
