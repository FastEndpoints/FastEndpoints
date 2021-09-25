using System.Linq.Expressions;
using System.Reflection;

namespace FastEndpoints
{
    internal static class ReflectionExtensions
    {
        internal static MemberInfo MemberInfo<T>(this Expression<T> expression)
        {
            return expression.Body switch
            {
                MemberExpression m => m.Member,
                UnaryExpression u when u.Operand is MemberExpression m => m.Member,
                _ => throw new NotSupportedException($"[{expression}] is not a valid member expression!"),
            };
        }

        internal static string PropertyName<T>(this Expression<T> expression)
            => MemberInfo(expression).Name;
    }
}
