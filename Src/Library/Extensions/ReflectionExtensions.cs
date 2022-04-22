using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

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
            throw new ArgumentException($"{nameof(targetGeneric)} is not a valid generic type", nameof(targetGeneric));

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

    private static readonly MethodInfo toStringMethod = Types.Object.GetMethod("ToString")!;
    private static readonly ConstructorInfo valueTupleConstructor = typeof(ValueTuple<bool, object>).GetConstructor(new[] { Types.Bool, Types.Object })!;
    private static Func<object?, (bool isSuccess, object value)>? GetCompiledValueParser(Type type)
    {
        // this method was contributed by: https://stackoverflow.com/users/1086121/canton7
        // as an answer to a stackoverflow question: https://stackoverflow.com/questions/71220157
        // many thanks to canton7 :-)

        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == Types.String)
            return input => (true, input!);

        if (type.IsEnum)
            return input => (Enum.TryParse(type, input?.ToString(), out var res), res!);

        if (type == Types.Uri)
            return input => (true, new Uri((string)input!));

        var tryParseMethod = type.GetMethod("TryParse", BindingFlags.Public | BindingFlags.Static, new[] { Types.String, type.MakeByRefType() });
        if (tryParseMethod == null || tryParseMethod.ReturnType != Types.Bool)
            return null;

        // The 'object' parameter passed into our delegate
        var inputParameter = Expression.Parameter(Types.Object, "input");

        // 'input == null ? (string)null : input.ToString()'
        var toStringConversion = Expression.Condition(
            Expression.ReferenceEqual(inputParameter, Expression.Constant(null, Types.Object)),
            Expression.Constant(null, Types.String),
            Expression.Call(inputParameter, toStringMethod));

        // 'res' variable used as the out parameter to the TryParse call
        var resultVar = Expression.Variable(type, "res");

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
}