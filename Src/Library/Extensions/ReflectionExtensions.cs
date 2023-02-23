using System.Linq.Expressions;

namespace FastEndpoints;

internal static class ReflectionExtensions
{
    internal static IEnumerable<string> PropNames<T>(this Expression<Func<T, object>> expression)
    {
        return expression?.Body is not NewExpression newExp
                ? throw new NotSupportedException($"[{expression}] is not a valid `new` expression!")
                : newExp.Arguments.Select(a => a.ToString().Split('.')[1]);
    }

    internal static string PropertyName<T>(this Expression<T> expression) => (
        expression.Body switch
        {
            MemberExpression m => m.Member,
            UnaryExpression u when u.Operand is MemberExpression m => m.Member,
            _ => throw new NotSupportedException($"[{expression}] is not a valid member expression!"),
        }).Name;

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
}