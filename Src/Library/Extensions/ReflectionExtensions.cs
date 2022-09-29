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

    private static readonly ConcurrentDictionary<Type, Func<StringValues, JsonNode?>> queryParsers = new();
    internal static Func<StringValues, JsonNode?> QueryValueParser(this Type type)
    {
        // almost the same parser logic but for nested query params
        return queryParsers.GetOrAdd(type, GetCompiledQueryValueParser);
    }

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

    private static Func<StringValues, JsonNode?> GetCompiledQueryValueParser(Type tProp)
    {
        tProp = Nullable.GetUnderlyingType(tProp) ?? tProp;
        if (tProp == Types.String)
            return input => input[0];

        if (tProp == Types.Bool)
            return input => bool.TryParse(input[0], out var res) ? res : input[0];

        if (tProp.IsEnum)
            return input => Enum.TryParse(tProp, input[0], true, out var res) ? JsonValue.Create(res) : input[0];
        if (tProp.GetInterfaces().Contains(Types.IEnumerable))
        {
            var elementType = tProp.GetGenericArguments().FirstOrDefault();
            var parser = elementType?.QueryValueParser();
            return parser is null
                ? input => new JsonArray().SetValues(input.Cast<JsonNode>())
                : input => new JsonArray().SetValues(input.Select(x => parser(x)));
        }
        return input => input[0];

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
