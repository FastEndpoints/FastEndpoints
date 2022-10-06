using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using static FastEndpoints.Config;

namespace FastEndpoints;

internal static class ReflectionExtensions
{
    internal static string PropertyName<T>(this Expression<T> expression) => (
        expression.Body switch
        {
            MemberExpression m => m.Member,
            UnaryExpression u when u.Operand is MemberExpression m => m.Member,
            _ => throw new NotSupportedException($"[{expression}] is not a valid member expression!"),
        }).Name;

    internal static Func<object, object> GetterForProp(this Type source, string propertyName)
    {
        //(object parent, object returnVal) => ((object)((TParent)parent).property);

        var parent = Expression.Parameter(Types.Object);
        var property = Expression.Property(Expression.Convert(parent, source), propertyName);
        var convertProp = Expression.Convert(property, Types.Object);

        return Expression.Lambda<Func<object, object>>(convertProp, parent).Compile();
    }

    internal static Action<object, object> SetterForProp(this Type source, string propertyName)
    {
        //(object parent, object value) => ((TParent)parent).property = (TProp)value;

        var parent = Expression.Parameter(Types.Object);
        var value = Expression.Parameter(Types.Object);
        var property = Expression.Property(Expression.Convert(parent, source), propertyName);
        var body = Expression.Assign(property, Expression.Convert(value, property.Type));

        return Expression.Lambda<Action<object, object>>(body, parent, value).Compile();
    }

    internal static Type[]? GetGenericArgumentsOfType(this Type source, Type targetGeneric)
    {
        if (!targetGeneric.IsGenericType)
            throw new ArgumentException($"{nameof(targetGeneric)} is not a valid generic type!", nameof(targetGeneric));

        var t = source;

        while (t != null)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == targetGeneric)
                return t.GetGenericArguments();

            t = t.BaseType;
        }

        return null;
    }

    private static readonly ConcurrentDictionary<Type, Func<object?, (bool isSuccess, object value)>?> parsers = new();
    internal static Func<object?, (bool isSuccess, object value)>? ValueParser(this Type type)
    {
        //we're only ever compiling a value parser for a given type once.
        //if a parser is requested for a type a second time, it will be returned from the dictionary instead of paying the compiling cost again.
        //the parser we return from here is then cached in ReqTypeCache<TRequest> avoiding the need to do an additional dictionary lookup here.
        return parsers.GetOrAdd(type, GetCompiledValueParser);
    }

    //private static readonly ConcurrentDictionary<Type, Action<IReadOnlyDictionary<string, StringValues>, JsonObject>> queryObjects = new();
    internal static Action<IReadOnlyDictionary<string, StringValues>, JsonObject> QueryObjectSetter(this Type type)
    {
        //NOTES: caching this action in queryObjects dictionary is pointless because the following reflection use is gonna run on each request.
        //TODO: this method can now become a static void. no need to allocate a delegate.

        return (queryString, obj) =>
        {
            //NOTES: moved here to avoid allocation of a closure
            var setters = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                .Select(prop => prop.PropertyType.GetQueryObjectSetter(prop.Name));

            var swaggerStyle = !queryString.Any(x => x.Key.Contains('.'));

            foreach (var setter in setters)
                setter(queryString, obj, null, swaggerStyle);
        };
    }

    //NOTES: no point in caching in a dictionary because no expression compiling is done inside the func

    //private static readonly ConcurrentDictionary<Type, Func<string, JsonNode?>?> queryParsers = new();
    //private static Func<string, JsonNode?>? QueryValueParser(this Type type)
    //{
    //    // almost the same parser logic but for nested query params
    //    return queryParsers.GetOrAdd(type, GetCompiledQueryValueParser);
    //}

    private static readonly MethodInfo toStringMethod = Types.Object.GetMethod("ToString")!;
    private static readonly ConstructorInfo valueTupleConstructor = typeof(ValueTuple<bool, object>).GetConstructor(new[] { Types.Bool, Types.Object })!;
    private static Func<object?, (bool isSuccess, object value)>? GetCompiledValueParser(Type tProp)
    {
        // this method was contributed by: https://stackoverflow.com/users/1086121/canton7
        // as an answer to a stackoverflow question: https://stackoverflow.com/questions/71220157
        // many thanks to canton7 :-)

        tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;

        //note: the actual type of the `input` to the parser func can be
        //      either [object] or [StringValues]

        if (tProp == Types.String)
            return input => (true, input?.ToString()!);

        if (tProp.IsEnum)
            return input => (Enum.TryParse(tProp, input?.ToString(), true, out var res), res!);

        if (tProp == Types.Uri)
            return input => (Uri.TryCreate(input?.ToString(), UriKind.Absolute, out var res), res!);

        var tryParseMethod = tProp.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, new[] { Types.String, tProp.MakeByRefType() });
        if (tryParseMethod == null || tryParseMethod.ReturnType != Types.Bool)
        {
            return tProp.GetInterfaces().Contains(Types.IEnumerable)
                   ? (input => (true, DeserializeJsonArrayString(input, tProp))!)
                   : (input => (true, DeserializeJsonObjectString(input, tProp))!);
        }

        // The 'object' parameter passed into our delegate
        var inputParameter = Expression.Parameter(Types.Object, "input");

        // 'input == null ? (string)null : input.ToString()'
        var toStringConversion = Expression.Condition(
            Expression.ReferenceEqual(inputParameter, Expression.Constant(null, Types.Object)),
            Expression.Constant(null, Types.String),
            Expression.Call(inputParameter, toStringMethod));

        // 'res' variable used as the out parameter to the TryParse call
        var resultVar = Expression.Variable(tProp, "res");

        // 'isSuccess' variable to hold the result of calling TryParse
        var isSuccessVar = Expression.Variable(Types.Bool, "isSuccess");

        // To finish off, we need to following sequence of statements:
        //  - isSuccess = TryParse(input.ToString(), res)
        //  - new ValueTuple<bool, object>(isSuccess, (object)res)
        // A sequence of statements is done using a block, and the result of the final
        // statement is the result of the block
        var tryParseCall = Expression.Call(tryParseMethod, toStringConversion, resultVar);
        var block = Expression.Block(new[] { resultVar, isSuccessVar },
            Expression.Assign(isSuccessVar, tryParseCall),
            Expression.New(valueTupleConstructor, isSuccessVar, Expression.Convert(resultVar, Types.Object)));

        return Expression.Lambda<Func<object?, (bool, object)>>(
            block,
            inputParameter
        ).Compile();
    }

    private static Action<IReadOnlyDictionary<string, StringValues>, JsonObject, string?, bool> GetQueryObjectSetter(this Type tProp, string propName)
    {
        //TODO: should convert to just a static void. since no point in this being a action.
        //      all reflection below gonna run on each iteration anyway.
        //      caching only makes sense if we're compiling expressions and what's being done is not doing reflection stuff.

        tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;
        if (!tProp.IsEnum && tProp != Types.String && tProp != Types.Bool)
        {
            if (tProp.GetInterfaces().Contains(Types.IEnumerable))
            {
                var arraySetter = tProp.GetQueryArraySetter();
                if (arraySetter != null)
                {
                    return (queryString, parent, route, swaggerStyle) =>
                    {
                        var array = new JsonArray();
                        parent[propName] = array;
                        route = route != null
                            ? swaggerStyle
                                ? $"{route}[{propName}]"
                                : $"{route}.{propName}"
                            : propName;
                        arraySetter(queryString, array, route, swaggerStyle);
                    };
                }
            }
            var props = tProp.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (props.Length > 0)
            {
                var setters = props.Select(prop => prop.PropertyType.GetQueryObjectSetter(prop.Name)).ToArray();

                return (queryString, parent, route, swaggerStyle) =>
                {
                    var obj = new JsonObject();
                    parent[propName] = obj;
                    foreach (var setter in setters)
                    {
                        setter(queryString, obj,
                            route != null
                                ? swaggerStyle
                                    ? $"{route}[{propName}]"
                                    : $"{route}.{propName}"
                                : propName,
                            swaggerStyle);
                    }
                };
            }
        }
        var parser = tProp == Types.Bool || tProp.IsEnum ? tProp.QueryValueParser() : null;
        if (parser == null)
        {
            return (queryString, parent, route, swaggerStyle) =>
            {
                route = route != null
                        ? swaggerStyle
                            ? $"{route}[{propName}]"
                            : $"{route}.{propName}"
                        : propName;
                if (queryString.TryGetValue(route, out var values))
                    parent[propName] = values[0];
            };
        }

        return (queryString, parent, route, swaggerStyle) =>
        {
            route = route != null
                    ? swaggerStyle
                        ? $"{route}[{propName}]"
                        : $"{route}.{propName}"
                    : propName;
            if (queryString.TryGetValue(route, out var values))
                parent[propName] = parser(values[0]);
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

    private static Action<IReadOnlyDictionary<string, StringValues>, JsonArray, string, bool>? GetQueryArraySetter(this Type type)
    {
        //TODO: same story as above. convert to a static void.

        var tProp = type.GetGenericArguments().FirstOrDefault();

        if (tProp == null)
            return null;

        tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;

        if (!tProp.IsEnum && tProp != Types.String && tProp != Types.Bool)
        {
            if (tProp.GetInterfaces().Contains(Types.IEnumerable))
            {
                var setter = GetQueryArraySetter(tProp);

                if (setter == null)
                    return null;

                return (queryString, parent, route, swaggerStyle) =>
                {
                    var array = new JsonArray();
                    parent.Add(array);
                    if (swaggerStyle)
                        return;
                    var keysCount = queryString.Count(x => x.Key.StartsWith(route, StringComparison.OrdinalIgnoreCase));
                    if (keysCount > 0)
                    {
                        for (var i = 0; i < keysCount; i++)
                            setter(queryString, array, $"{route}[{i}]", swaggerStyle);
                    }
                };
            }
            var props = tProp.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (props.Length > 0)
            {
                var setters = props.Select(prop => prop.PropertyType.GetQueryObjectSetter(prop.Name));

                return (queryString, parent, route, swaggerStyle) =>
                {
                    var obj = new JsonObject();
                    parent.Add(obj);

                    var keysCount = queryString.Count(x => x.Key.StartsWith(route, StringComparison.OrdinalIgnoreCase));
                    if (keysCount > 0)
                    {
                        for (var i = 0; i < keysCount; i++)
                        {
                            foreach (var setter in setters)
                                setter(queryString, obj, $"{route}[{i}]", swaggerStyle);
                        }
                    }
                };
            }
        }
        var parser = tProp == Types.Bool || tProp.IsEnum ? tProp.QueryValueParser() : null;
        if (parser == null)
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

                for (var i = 0; queryString.TryGetValue($"{route}[{i}]", out var svalues); i++)
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
                        parent.Add(parser(value));
                }
                return;
            }

            for (var i = 0; queryString.TryGetValue($"{route}[{i}]", out var svalues); i++)
                parent.Add(parser(svalues[0]));
        };
    }

    private static object? DeserializeJsonObjectString(object? input, Type tProp)
    {
        if (input is not StringValues vals || vals.Count == 0)
            return null;

        if (vals.Count == 1 && vals[0].StartsWith('{') && vals[0].EndsWith('}'))
        {
            // {"name":"x","age":24}
            return JsonSerializer.Deserialize(vals[0], tProp, SerOpts.Options);
        }
        return null;
    }

    private static object? DeserializeJsonArrayString(object? input, Type tProp)
    {
        if (input is not StringValues vals || vals.Count == 0)
            return null;

        if (vals.Count == 1 && vals[0].StartsWith('[') && vals[0].EndsWith(']'))
        {
            // querystring: ?ids=[1,2,3]
            // possible inputs:
            // - [1,2,3] (as StringValues[0])
            // - ["one","two","three"] (as StringValues[0])
            // - [{"name":"x"},{"name":"y"}] (as StringValues[0])

            return JsonSerializer.Deserialize(vals[0], tProp, SerOpts.Options);
        }

        // querystring: ?ids=one&ids=two
        // possible inputs:
        // - 1 (as StringValues)
        // - 1,2,3 (as StringValues)
        // - one (as StringValues)
        // - one,two,three (as StringValues)
        // - [1,2], 2, 3 (as StringValues)
        // - ["one","two"], three, four (as StringValues)
        // - {"name":"x"}, {"name":"y"} (as StringValues) - from swagger ui

        var sb = new StringBuilder("[");
        for (var i = 0; i < vals.Count; i++)
        {
            if (vals[i].StartsWith('{') && vals[i].EndsWith('}'))
            {
                sb.Append(vals[i]);
            }
            else
            {
                sb.Append('"')
                  .Append(
                    vals[i].Contains('"') //json strings with quotations must be escaped
                    ? vals[i].Replace("\"", "\\\"")
                    : vals[i])
                  .Append('"');
            }

            if (i < vals.Count - 1)
                sb.Append(',');
        }
        sb.Append(']');

        return JsonSerializer.Deserialize(sb.ToString(), tProp, SerOpts.Options);
    }
}
