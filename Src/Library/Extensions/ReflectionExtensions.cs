using System.Linq.Expressions;

namespace FastEndpoints;

static class ReflectionExtensions
{
    internal static IEnumerable<string> PropNames<T>(this Expression<Func<T, object>> expression)
    {
        return expression.Body is not NewExpression newExp
                   ? throw new NotSupportedException($"[{expression}] is not a valid `new` expression!")
                   : newExp.Arguments.Select(a => a.ToString().Split('.')[1]);
    }

    internal static string PropertyName<T>(this Expression<T> expression)
        => (expression.Body switch
               {
                   MemberExpression m => m.Member,
                   UnaryExpression { Operand: MemberExpression m } => m.Member,
                   _ => throw new NotSupportedException($"[{expression}] is not a valid member expression!")
               }).Name;
    
    internal static string FullPropertyChain<T>(this Expression<T> expression)
        => expression.Body.FullPropertyChain();
    
    internal static string FullPropertyChain(this Expression expression)
    {
        return expression switch
        {
            MemberExpression m => BuildMemberChain(m),
            BinaryExpression { NodeType: ExpressionType.ArrayIndex } be => GetArrayIndexExpression(be),
            MethodCallExpression mce when IsItemAccessor(mce) => GetItemAccessorExpression(mce),
            UnaryExpression ue => FullPropertyChain(ue.Operand),
            _ => throw new NotSupportedException($"[{expression}] is not a supported expression type!")
        };
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
    
    // Helpers

    static bool IsItemAccessor(MethodCallExpression mce)
        => mce.Method.Name == "get_Item" && mce.Arguments.Count == 1;

    static string GetItemAccessorExpression(MethodCallExpression mce) 
        => FormatIndexerExpression(mce.Object, mce.Arguments[0]);

    static string GetArrayIndexExpression(BinaryExpression be) 
        => FormatIndexerExpression(be.Left, be.Right);

    static string FormatIndexerExpression(Expression? objectExpression, Expression indexExpression)
    {
        var objectChain = objectExpression?.FullPropertyChain();
        var indexValue = EvaluateExpression(indexExpression);
        return $"{objectChain}[{indexValue}]";
    }

    static string EvaluateExpression(Expression expression)
    {
        try
        {
            return Expression.Lambda(expression).Compile().DynamicInvoke() switch
            {
                string s => $"\"{s}\"",
                var o => o?.ToString()
            } ?? throw new ArgumentNullException(nameof(expression), "The evaluated expression resulted in null.");
        }
        catch (Exception ex)
        {
            throw new NotSupportedException("Failed to evaluate expression.", ex);
        }
    }
    
    static string BuildMemberChain(MemberExpression memberExpression) 
        => memberExpression.Expression is null or ParameterExpression
            ? memberExpression.Member.Name 
            : $"{FullPropertyChain(memberExpression.Expression)}.{memberExpression.Member.Name}";
}