using System.Linq.Expressions;

namespace FastEndpoints
{
    internal static class ReflectionExtensions
    {
        internal static string PropertyName<T>(this Expression<T> expression) => (
            expression.Body switch
            {
                MemberExpression m => m.Member,
                UnaryExpression u when u.Operand is MemberExpression m => m.Member,
                _ => throw new NotSupportedException($"[{expression}] is not a valid member expression!"),
            }).Name;
    }
}
