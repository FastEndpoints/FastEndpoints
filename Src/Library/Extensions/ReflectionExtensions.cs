using System.Linq.Expressions;
using System.Reflection;

namespace FastEndpoints;

static class ReflectionExtensions
{
    internal static string FieldName(this PropertyInfo p)
        => p.GetCustomAttribute<BindFromAttribute>()?.Name ??
           (Cfg.BndOpts.UsePropertyNamingPolicy && Cfg.SerOpts.Options.PropertyNamingPolicy is not null
                ? Cfg.SerOpts.Options.PropertyNamingPolicy.ConvertName(p.Name)
                : p.Name);

    internal static IEnumerable<string> PropNames<T>(this Expression<Func<T, object>> expression)
    {
        return expression.Body is not NewExpression newExp
                   ? throw new NotSupportedException($"[{expression}] is not a valid `new` expression!")
                   : newExp.Arguments.Select(
                       a =>
                       {
                           if (a is not MemberExpression m)
                               throw new InvalidOperationException("not a member expression!");

                           return m.Member.IsDefined(typeof(BindFromAttribute))
                                      ? m.Member.GetCustomAttribute<BindFromAttribute>()!.Name
                                      : a.ToString().Split('.')[1];
                       });
    }

    internal static string PropertyName<T>(this Expression<T> expression)
        => (expression.Body switch
               {
                   MemberExpression m => m.Member,
                   UnaryExpression { Operand: MemberExpression m } => m.Member,
                   _ => throw new NotSupportedException($"[{expression}] is not a valid member expression!")
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

    internal static Type GetUnderlyingType(this Type type)
        => Nullable.GetUnderlyingType(type) ?? type;

    internal static bool IsComplexType(this Type type)
    {
        var isComplex = type.IsClass || (type.IsValueType && type is { IsPrimitive: false, IsEnum: false } && type != typeof(decimal));
        var isSimpleType = type == typeof(string) ||
                           type == typeof(DateTime) ||
                           type == typeof(DateTimeOffset) ||
                           type == typeof(TimeSpan) ||
                           type == typeof(Guid) ||
                           type == typeof(Uri) ||
                           type == typeof(Version);

        return isComplex && !isSimpleType;
    }

    internal static bool IsCollection(this Type type)
        => Types.IEnumerable.IsAssignableFrom(type) && type != Types.String;

    internal static bool IsFormFileProp(this Type tProp)
        => Types.IFormFile.IsAssignableFrom(tProp);

    internal static bool IsFormFileCollectionProp(this Type tProp)
        => Types.IEnumerableOfIFormFile.IsAssignableFrom(tProp);

    internal static bool IsValidatable(this Type type)
    {
        var typeDef = Cfg.BndOpts.ReflectionCache.GetOrAdd(type, new TypeDefinition());

        if (typeDef.IsValidatable is null) // was never initialized
        {
            if (type.IsComplexType() && (type.IsDefined(Types.ValidationAttribute) || type.BindableProps().Any(p => p.IsDefined(Types.ValidationAttribute))))
                typeDef.IsValidatable = true;
            else
                typeDef.IsValidatable = false;
        }

        return typeDef.IsValidatable is true;
    }
}