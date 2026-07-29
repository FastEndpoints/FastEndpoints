using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

static class EndpointWarmup
{
    [UnconditionalSuppressMessage("aot", "IL2055"), UnconditionalSuppressMessage("aot", "IL3050")]
    internal static void WarmupEndpoint(EndpointDefinition def, IServiceProvider sp)
    {
        if (!def.ReqDtoType.IsValueType) // native aot cannot instantiate value type generic binders
            _ = sp.GetService(Types.IRequestBinderOf1.MakeGenericType(def.ReqDtoType));

        PrecompileValidatableType(def.ReqDtoType, []);

        _ = def.ExecuteAsyncReturnsIResult;
        _ = def.GetMapper();
        _ = def.GetValidator();
        _ = def.ReqDtoFromBodyPropName;
        _ = def.ReqDtoType.ObjectFactory();
        _ = def.ToHeaderProps;
    }

    static void PrecompileValidatableType(Type type, HashSet<Type> visited)
    {
        // Only precompile what ValidateRecursively uses: bindable props + getters.
        // Nested ObjectFactory/setters belong to binding and can fail for abstract /
        // non-constructible declared types that runtime validation still supports.
        if (!type.IsValidatable() || !visited.Add(type))
            return;

        foreach (var prop in type.BindableProps())
        {
            _ = type.GetterForProp(prop);

            var nested = prop.PropertyType.IsCollection()
                             ? prop.PropertyType.GetCollectionElementType()
                             : prop.PropertyType;

            if (nested is not null)
                PrecompileValidatableType(nested, visited);
        }
    }
}
