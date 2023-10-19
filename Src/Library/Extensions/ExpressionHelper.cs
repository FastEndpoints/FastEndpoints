using System.Linq.Expressions;
using System.Reflection;

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

    static bool IsItemAccessor(MethodCallExpression mce)
        => mce.Method.Name == "get_Item" && mce.Arguments.Count == 1;

    static string FormatIndexerExpression(Expression objectExpression, Expression indexExpression)
    {
        if (objectExpression is null)
            throw new ArgumentNullException(nameof(objectExpression), "Object expression cannot be null.");

        return $"{GetPropertyChain(objectExpression)}[{GetIndexerValueText(indexExpression)}]";
    }

    static string? GetIndexerValueText(Expression expression)
        => GetValue(expression) switch
        {
            string s => $"\"{s}\"",
            var v => v?.ToString()
        };

    static string BuildMemberChain(MemberExpression memberExpression)
        => memberExpression.Expression is null or ParameterExpression
            ? memberExpression.Member.Name
            : $"{GetPropertyChain(memberExpression.Expression)}.{memberExpression.Member.Name}";
    
    
    
    internal static object? GetValue(Expression? expression)
        => expression switch
        {
            null => throw new ArgumentNullException(nameof(expression), "Expression cannot be null."),
            ConstantExpression ce => ce.Value,
            MemberExpression me => GetValue(me),
            MethodCallExpression mce => GetValue(mce),
            _ => GetValueCompiled(expression)
        };

    static object? GetValue(MemberExpression expression)
    {
        var value = GetValue(expression.Expression);
        return expression.Member switch
        {
            FieldInfo fi => fi.GetValue(value),
            PropertyInfo pi => pi.GetValue(value),
            _ => throw new NotSupportedException($"[{expression}] is not a supported member expression!")
        };
    }
    
    static object? GetValue(MethodCallExpression expression)
    {
        var args = expression.Arguments.Select(GetValue).ToArray();
        var obj = GetValue(expression.Object);
        return expression.Method.Invoke(obj, args);
    }

    static object? GetValueCompiled(Expression expression)
    {
        return Expression.Lambda(expression).Compile().DynamicInvoke();
    }
}
