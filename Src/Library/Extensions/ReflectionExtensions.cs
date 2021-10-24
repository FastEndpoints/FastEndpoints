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

        var sourceObjectParam = Expression.Parameter(typeof(object), "source");

#pragma warning disable CS8602,CS8604
        Expression returnExpression = Expression.Call(
            Expression.Convert(sourceObjectParam, source),
            propertyInfo.GetGetMethod());
#pragma warning restore CS8602,CS8604

        if (!propertyInfo.PropertyType.IsClass)
            returnExpression = Expression.Convert(returnExpression, typeof(object));

        return Expression.Lambda<Func<object, object>>(returnExpression, sourceObjectParam).Compile();
    }

    internal static Action<object, object> SetterForProp(this Type source, string propertyName)
    {
        var propertyInfo = source.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (propertyInfo?.CanWrite != true)
            throw new InvalidOperationException($"[{source.FullName}.{propertyName}] is not writable!");

        var sourceObjectParam = Expression.Parameter(typeof(object), "source");

        var propertyValueParam = Expression.Parameter(typeof(object), "value");

#pragma warning disable CS8602, CS8604
        var valueExpression = Expression.Convert(propertyValueParam, propertyInfo.PropertyType);

        return Expression.Lambda<Action<object, object>>(
            Expression.Call(
                Expression.Convert(sourceObjectParam, source),
                propertyInfo.GetSetMethod(),
                valueExpression),
            sourceObjectParam,
            propertyValueParam).Compile();
#pragma warning restore CS8602, CS8604
    }
}

