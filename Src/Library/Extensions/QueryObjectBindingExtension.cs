using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Nodes;

namespace FastEndpoints;

internal static class QueryObjectBindingExtension
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]?> propCache = new();
    private static PropertyInfo[] AllProperties(this Type type)
        => propCache.GetOrAdd(type, type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy) ?? Array.Empty<PropertyInfo>())!;

    private static readonly ConcurrentDictionary<Type, Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, string?, bool>> queryObjects = new();
    internal static Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, string?, bool> QueryObjectSetter(this Type type)
    {
        return queryObjects.GetOrAdd(type, GetQueryObjectSetter(type));

        static Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, string?, bool> GetQueryObjectSetter(Type tProp)
        {
            tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;
            if (!tProp.IsEnum && tProp != Types.String && tProp != Types.Bool)
            {
                if (tProp.GetInterfaces().Contains(Types.IEnumerable))
                {
                    return (queryString, parent, route, propName, swaggerStyle) =>
                    {
                        var array = new JsonArray();
                        propName ??= Constants.QueryJsonNodeName;
                        parent[propName] = array;
                        route = route != null
                                ? swaggerStyle
                                  ? $"{route}[{propName}]"
                                  : $"{route}.{propName}"
                                : propName;
                        tProp.QueryArraySetter()?.Invoke(queryString, array, route, swaggerStyle);
                    };
                }

                if (tProp.AllProperties().Length > 0)
                {
                    return (queryString, parent, route, propName, swaggerStyle) =>
                    {
                        var obj = new JsonObject();
                        var usePropName = propName != null;
                        parent[usePropName ? propName! : Constants.QueryJsonNodeName] = obj;
                        route = usePropName
                                ? route != null
                                  ? swaggerStyle
                                    ? $"{route}[{propName}]"
                                    : $"{route}.{propName}"
                                  : propName
                                : null;

                        var props = tProp.AllProperties();
                        for (var i = 0; i < props?.Length; i++)
                            props[i].PropertyType.QueryObjectSetter()(queryString, obj, route, props[i].Name, swaggerStyle);
                    };
                }
            }

            if (tProp != Types.Bool && !tProp.IsEnum)
            {
                return (queryString, parent, route, propName, swaggerStyle) =>
                {
                    route = route != null
                            ? swaggerStyle
                              ? $"{route}[{propName}]"
                              : $"{route}.{propName}"
                            : propName;
                    if (queryString.TryGetValue(route!, out var values))
                        parent[propName!] = values[0];
                };
            }

            return (queryString, parent, route, propName, swaggerStyle) =>
            {
                route = route != null
                        ? swaggerStyle
                          ? $"{route}[{propName}]"
                          : $"{route}.{propName}"
                        : propName;

                if (queryString.TryGetValue(route!, out var values))
                    parent[propName!] = tProp.QueryValueParser()!.Invoke(values[0]);
            };
        }
    }

    private static readonly ConcurrentDictionary<Type, Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string, bool>?> queryArrays = new();
    private static Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string, bool>? QueryArraySetter(this Type type)
    {
        return queryArrays.GetOrAdd(type, GetQueryArraySetter(type));

        static Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string, bool>? GetQueryArraySetter(Type type)
        {
            var tProp = type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault();

            if (tProp == null)
                return null;

            tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;

            if (!tProp.IsEnum && tProp != Types.String && tProp != Types.Bool)
            {
                if (tProp.GetInterfaces().Contains(Types.IEnumerable))
                {
                    if (tProp.QueryArraySetter() is null)
                        return null;

                    return (queryString, parent, route, swaggerStyle) =>
                    {
                        if (swaggerStyle)
                            return;

                        var i = 0;
                        var newRoute = $"{route}[0]";

                        while (queryString.Any(x => x.Key.StartsWith(newRoute, StringComparison.OrdinalIgnoreCase)))
                        {
                            var array = new JsonArray();
                            parent.Add(array);
                            tProp.QueryArraySetter()?.Invoke(queryString, array, newRoute, swaggerStyle);
                            i++;
                            newRoute = $"{route}[" + i.ToString() + "]";
                        }
                    };
                }

                if (tProp.AllProperties().Length > 0)
                {
                    return (queryString, parent, route, swaggerStyle) =>
                    {
                        if (swaggerStyle)
                            return;

                        var i = 0;
                        var newRoute = $"{route}[0]";

                        while (queryString.Any(x => x.Key.StartsWith(newRoute, StringComparison.OrdinalIgnoreCase)))
                        {
                            var obj = new JsonObject();
                            parent.Add(obj);
                            foreach (var p in tProp.AllProperties())
                                p.PropertyType.QueryObjectSetter()(queryString, obj, newRoute, p.Name, swaggerStyle);
                            i++;
                            newRoute = string.Concat(route, "[", i, "]");
                        }
                    };
                }
            }

            if (tProp != Types.Bool && !tProp.IsEnum)
            {
                return (queryString, parent, route, swaggerStyle) =>
                {
                    if (swaggerStyle)
                    {
                        if (queryString.TryGetValue(route, out var values))
                        {
                            foreach (var value in values)
                                parent.Add(value);
                        }
                        return;
                    }

                    for (var i = 0; queryString.TryGetValue($"{route}[" + i.ToString() + "]", out var svalues); i++)
                        parent.Add(svalues[0]);
                };
            }

            return (queryString, parent, route, swaggerStyle) =>
            {
                if (swaggerStyle)
                {
                    if (queryString.TryGetValue(route, out var values))
                    {
                        foreach (var value in values)
                            parent.Add(tProp.QueryValueParser()?.Invoke(value));
                    }
                    return;
                }

                for (var i = 0; queryString.TryGetValue($"{route}[" + i.ToString() + "]", out var svalues); i++)
                    parent.Add(tProp.QueryValueParser()?.Invoke(svalues[0]));
            };
        }
    }

    private static Func<string?, JsonNode?>? QueryValueParser(this Type tProp)
    {
        if (tProp == Types.Bool)
            return input => bool.TryParse(input, out var res) ? res : input;

        if (tProp.IsEnum)
            return input => Enum.TryParse(tProp, input, true, out var res) ? JsonValue.Create(res) : input;

        return null;
    }
}