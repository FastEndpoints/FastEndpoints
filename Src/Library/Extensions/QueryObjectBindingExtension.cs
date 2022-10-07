using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace FastEndpoints.Extensions;
internal static class QueryObjectBindingExtension
{

    private static readonly ConcurrentDictionary<Type, Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, string?, bool>> queryObjects = new();
    internal static Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, string?, bool> QueryObjectSetter(this Type type)
    {
        if (queryObjects.TryGetValue(type, out var setter))
        {
            return setter;
        }
        return queryObjects.GetOrAdd(type, GetQueryObjectSetter(type));
    }
    private static Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, string?, bool> GetQueryObjectSetter(Type tProp)
    {
        tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;
        if (!tProp.IsEnum && tProp != Types.String && tProp != Types.Bool)
        {
            if (tProp.GetInterfaces().Contains(Types.IEnumerable))
            {
                var arraySetter = tProp.QueryArraySetter();
                if (arraySetter != null)
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
                        arraySetter(queryString, array, route, propName, swaggerStyle);
                    };
                }
            }
            var props = tProp.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (props.Length > 0)
            {
                var setters = props.Select(prop => new { Setter = prop.PropertyType.QueryObjectSetter(), PropName = prop.Name }).ToArray();

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

                    foreach (var setter in setters)
                    {
                        setter.Setter(
                            queryString,
                            obj,
                            route,
                            setter.PropName,
                            swaggerStyle
                        );
                    }
                };
            }
        }
        var parser = tProp == Types.Bool || tProp.IsEnum ? tProp.QueryValueParser() : null;
        if (parser == null)
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
                parent[propName!] = parser(values[0]);
        };
    }

    private static Func<string?, JsonNode?>? QueryValueParser(this Type tProp)
    {
        if (tProp == Types.Bool)
            return input => bool.TryParse(input, out var res) ? res : input;

        if (tProp.IsEnum)
            return input => Enum.TryParse(tProp, input, true, out var res) ? JsonValue.Create(res) : input;
        return null;
    }

    private static readonly ConcurrentDictionary<Type, Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string?, string, bool>?> queryArrays = new();

    private static Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string?, string, bool>? QueryArraySetter(this Type type)
    {
        if (queryArrays.TryGetValue(type, out var setter))
        {
            return setter;
        }
        setter = GetQueryArraySetter(type);
        return queryArrays.GetOrAdd(type, setter);
    }
    private static Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string?, string, bool>? GetQueryArraySetter(Type type)
    {
        var tProp = type.GetElementType() ?? type.GetGenericArguments().FirstOrDefault();

        if (tProp == null)
            return null;

        tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;

        if (!tProp.IsEnum && tProp != Types.String && tProp != Types.Bool)
        {
            if (tProp.GetInterfaces().Contains(Types.IEnumerable))
            {
                var setter = tProp.QueryArraySetter();

                if (setter == null)
                    return null;

                return (queryString, parent, route, paramName, swaggerStyle) =>
                {
                    if (swaggerStyle)
                        return;

                    var i = 0;
                    var newRoute = $"{route ?? paramName}[0]";

                    while (queryString.Any(x => x.Key.StartsWith(newRoute, StringComparison.OrdinalIgnoreCase)))
                    {

                        var array = new JsonArray();
                        parent.Add(array);

                        setter(queryString, array, newRoute, paramName, swaggerStyle);
                        newRoute = $"{route ?? paramName}[{++i}]";
                    }
                };
            }
            var props = tProp.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (props.Length > 0)
            {
                var setters = props.Select(prop => new { Setter = prop.PropertyType.QueryObjectSetter(), PropName = prop.Name }).ToArray();

                return (queryString, parent, route, paramName, swaggerStyle) =>
                {
                    if (swaggerStyle)
                        return;
                    var i = 0;
                    var newRoute = $"{route ?? paramName}[0]";

                    while (queryString.Any(x => x.Key.StartsWith(newRoute, StringComparison.OrdinalIgnoreCase)))
                    {

                        var obj = new JsonObject();
                        parent.Add(obj);
                        foreach (var setter in setters)
                            setter.Setter(queryString, obj, newRoute, setter.PropName, swaggerStyle);
                        newRoute = $"{route ?? paramName}[{++i}]";
                    }
                };
            }
        }
        var parser = tProp == Types.Bool || tProp.IsEnum ? tProp.QueryValueParser() : null;
        if (parser == null)
        {
            return (queryString, parent, route, paramName, swaggerStyle) =>
            {
                if (swaggerStyle)
                {
                    if (queryString.TryGetValue(route ?? paramName, out var values))
                    {
                        foreach (var value in values)
                            parent.Add(value);
                    }
                    return;
                }

                for (var i = 0; queryString.TryGetValue($"{route ?? paramName}[{i}]", out var svalues); i++)
                    parent.Add(svalues[0]);
            };
        }

        return (queryString, parent, route, paramName, swaggerStyle) =>
        {
            if (swaggerStyle)
            {
                if (queryString.TryGetValue(route ?? paramName, out var values))
                {
                    foreach (var value in values)
                        parent.Add(parser(value));
                }
                return;
            }

            for (var i = 0; queryString.TryGetValue($"{route ?? paramName}[{i}]", out var svalues); i++)
                parent.Add(parser(svalues[0]));
        };
    }



}
