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

    internal static Func<object?, (bool isSuccess, object value)>? ValueParser(this Type type)
    {
        if (type.IsEnum)
            return input => (Enum.TryParse(type, ToString(input), out var res), res!);

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.String:
                return input => (true, input!);

            case TypeCode.Boolean:
                return input => (bool.TryParse(ToString(input), out var res), res);

            case TypeCode.Int32:
                return input => (int.TryParse(ToString(input), out var res), res);

            case TypeCode.Int64:
                return input => (long.TryParse(ToString(input), out var res), res);

            case TypeCode.Double:
                return input => (double.TryParse(ToString(input), out var res), res);

            case TypeCode.Decimal:
                return input => (decimal.TryParse(ToString(input), out var res), res);

            case TypeCode.DateTime:
                return input => (DateTime.TryParse(ToString(input), out var res), res);

            case TypeCode.Object:
                if (type == Types.Guid)
                {
                    return input => (Guid.TryParse(ToString(input), out var res), res);
                }
                else if (type == Types.Uri)
                {
                    return input => (true, new Uri((string)input!));
                }
                else if (type == Types.Version)
                {
                    return input => (Version.TryParse(ToString(input), out var res), res!);
                }
                else if (type == Types.TimeSpan)
                {
                    return input => (TimeSpan.TryParse(ToString(input), out var res), res!);
                }
                break;
        }

        return null;

        static string? ToString(object? value)
        {
            if (value is string x)
                return x;

            return value?.ToString();
        }
    }
}