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

    extension(Type source)
    {
        internal Type[]? GetGenericArgumentsOfType(Type targetGeneric)
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

        internal Type GetUnderlyingType()
            => Nullable.GetUnderlyingType(source) ?? source;

        internal bool IsComplexType()
        {
            var isComplex = source.IsClass || (source.IsValueType && source is { IsPrimitive: false, IsEnum: false } && source != typeof(decimal));
            var isSimpleType = source == typeof(string) ||
                               source == typeof(DateTime) ||
                               source == typeof(DateTimeOffset) ||
                               source == typeof(DateOnly) ||
                               source == typeof(TimeOnly) ||
                               source == typeof(TimeSpan) ||
                               source == typeof(Guid) ||
                               source == typeof(Half) ||
                               source == typeof(Int128) ||
                               source == typeof(UInt128) ||
                               source == typeof(System.Numerics.BigInteger) ||
                               source == typeof(System.Net.IPAddress) ||
                               source == typeof(System.Net.IPEndPoint) ||
                               source == typeof(Uri) ||
                               source == typeof(Version);

            return isComplex && !isSimpleType;
        }

        internal bool IsCollection()
            => Types.IEnumerable.IsAssignableFrom(source) && source != Types.String;

        internal bool IsFormFileProp()
            => Types.IFormFile.IsAssignableFrom(source);

        internal bool IsFormFileCollectionProp()
            => Types.IEnumerableOfIFormFile.IsAssignableFrom(source);

        internal bool IsValidatable()
        {
            var typeDef = Cfg.BndOpts.ReflectionCache.GetOrAdd(source, new TypeDefinition());

            if (typeDef.IsValidatable is null) // was never initialized
            {
                if (source.IsComplexType() && (source.IsDefined(Types.ValidationAttribute) || source.BindableProps().Any(p => p.IsDefined(Types.ValidationAttribute))))
                    typeDef.IsValidatable = true;
                else
                    typeDef.IsValidatable = false;
            }

            return typeDef.IsValidatable is true;
        }
    }
}