using System.Linq.Expressions;
using System.Reflection;

namespace FastEndpoints;

public static class Tool
{
    public static Func<T, object> CompiledGetter<T>(string propertyName)
    {
        var ownerExpression = Expression.Parameter(typeof(T), "owner");
        var propertyExpression = Expression.Property(ownerExpression, propertyName);
        return Expression.Lambda<Func<T, object>>(
            Expression.Convert(propertyExpression, typeof(object)),
            ownerExpression
            ).Compile();
    }

    public static Action<T, object> CompiledSetter<T>(string propertyName)
    {
#pragma warning disable CS8604
        var type = typeof(T);
        var propInfo = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
        var ownerExpression = Expression.Parameter(typeof(T));
        var propertyExpression = Expression.Parameter(typeof(object));
        return Expression.Lambda<Action<T, object>>(
            Expression.Assign(
                Expression.Property(Expression.Convert(ownerExpression, type), propInfo),
                Expression.Convert(propertyExpression, propInfo.PropertyType)),
            ownerExpression, propertyExpression).Compile();
#pragma warning restore CS8604
    }
}
