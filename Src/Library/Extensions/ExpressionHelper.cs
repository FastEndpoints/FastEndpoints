using System.Linq.Expressions;

namespace FastEndpoints;


internal static class ExpressionHelper
{
    internal static string GetPropertyChain(Expression expression)
    {
        return expression switch
        {
            MemberExpression m => BuildMemberChain(m),
            BinaryExpression { NodeType: ExpressionType.ArrayIndex } be => FormatIndexerExpression(be.Left, be.Right),
            MethodCallExpression { Object: not null } mce when IsItemAccessor(mce) => FormatIndexerExpression(mce.Object, mce.Arguments[0]),
            UnaryExpression ue => GetPropertyChain(ue.Operand),
            _ => throw new NotSupportedException($"[{expression}] is not a supported expression type!")
        };
    }

    private static bool IsItemAccessor(MethodCallExpression mce)
        => mce.Method.Name == "get_Item" && mce.Arguments.Count == 1;

    private static string FormatIndexerExpression(Expression objectExpression, Expression indexExpression)
    {
        if (objectExpression is null)
            throw new ArgumentNullException(nameof(objectExpression), "Object expression cannot be null.");

        return $"{GetPropertyChain(objectExpression)}[{EvaluateExpression(indexExpression)}]";
    }

    private static string EvaluateExpression(Expression expression)
        => Expression.Lambda(expression).Compile().DynamicInvoke() switch
        {
            null => throw new ArgumentNullException(nameof(expression), "The evaluated expression resulted in null."),
            string s => $"\"{s}\"",
            var v => v.ToString() ?? throw new InvalidOperationException("Value's ToString method returned null.")
        };

    private static string BuildMemberChain(MemberExpression memberExpression)
        => memberExpression.Expression is null or ParameterExpression
            ? memberExpression.Member.Name
            : $"{GetPropertyChain(memberExpression.Expression)}.{memberExpression.Member.Name}";
}
