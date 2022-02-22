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
        var propertyInfo = source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (propertyInfo?.CanRead != true)
            throw new InvalidOperationException($"[{source.FullName}.{propertyName}] is not readable!");

        var sourceObjectParam = Expression.Parameter(Types.Object, "source");

        Expression returnExpression = Expression.Call(
            Expression.Convert(sourceObjectParam, source),
            propertyInfo.GetGetMethod()!);

        if (!propertyInfo.PropertyType.IsClass)
            returnExpression = Expression.Convert(returnExpression, Types.Object);

        return Expression.Lambda<Func<object, object>>(returnExpression, sourceObjectParam).Compile();
    }

    internal static Action<object, object> SetterForProp(this Type source, string propertyName)
    {
        var propertyInfo = source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (propertyInfo?.CanWrite != true)
            throw new InvalidOperationException($"[{source.FullName}.{propertyName}] is not writable!");

        var sourceObjectParam = Expression.Parameter(Types.Object, "source");

        var propertyValueParam = Expression.Parameter(Types.Object, "value");

        var valueExpression = Expression.Convert(propertyValueParam, propertyInfo.PropertyType);

        return Expression.Lambda<Action<object, object>>(
            Expression.Call(
                Expression.Convert(sourceObjectParam, source),
                propertyInfo.GetSetMethod()!,
                valueExpression),
            sourceObjectParam,
            propertyValueParam).Compile();
    }

    private static readonly MethodInfo toStringMethod = Types.Object.GetMethod("ToString")!;
    private static readonly ConstructorInfo valueTupleConstructor = typeof(ValueTuple<bool, object>).GetConstructor(new[] { Types.Bool, Types.Object })!;

    internal static Func<object?, (bool isSuccess, object value)>? ValueParser(this Type type)
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
        var xxx = type.GetMethods();
        var yyy = type.GetRuntimeMethods();

        if (tryParseMethod == null || tryParseMethod.ReturnType != Types.Bool)
            return null;

        // The 'object' parameter passed into our delegate
        var inputParameter = Expression.Parameter(typeof(object), "input");

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

    //internal static Func<object?, (bool isSuccess, object value)>? ValueParser(this Type type)
    //{
    //    type = Nullable.GetUnderlyingType(type) ?? type;

    //    if (type.IsEnum)
    //        return input => (Enum.TryParse(type, ToString(input), out var res), res!);

    //    switch (Type.GetTypeCode(type))
    //    {
    //        case TypeCode.String:
    //            return input => (true, input!);

    //        case TypeCode.Boolean:
    //            return input => (bool.TryParse(ToString(input), out var res), res);

    //        case TypeCode.Int32:
    //            return input => (int.TryParse(ToString(input), out var res), res);

    //        case TypeCode.Int64:
    //            return input => (long.TryParse(ToString(input), out var res), res);

    //        case TypeCode.Double:
    //            return input => (double.TryParse(ToString(input), out var res), res);

    //        case TypeCode.Decimal:
    //            return input => (decimal.TryParse(ToString(input), out var res), res);

    //        case TypeCode.DateTime:
    //            return input => (DateTime.TryParse(ToString(input), out var res), res);

    //        case TypeCode.Object:
    //            if (type == Types.Guid)
    //            {
    //                return input => (Guid.TryParse(ToString(input), out var res), res);
    //            }
    //            else if (type == Types.Uri)
    //            {
    //                return input => (true, new Uri((string)input!));
    //            }
    //            else if (type == Types.Version)
    //            {
    //                return input => (Version.TryParse(ToString(input), out var res), res!);
    //            }
    //            else if (type == Types.TimeSpan)
    //            {
    //                return input => (TimeSpan.TryParse(ToString(input), out var res), res!);
    //            }
    //            break;
    //    }

    //    return null; //we cannot handle this property type.

    //    static string? ToString(object? value)
    //    {
    //        if (value is string x)
    //            return x;

    //        return value?.ToString();
    //    }
    //}
}