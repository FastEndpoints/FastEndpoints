using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace FastEndpoints;

static class EndpointWarmup
{
    [UnconditionalSuppressMessage("aot", "IL2055"), UnconditionalSuppressMessage("aot", "IL3050")]
    internal static void WarmupEndpoint(EndpointDefinition def, IServiceProvider sp)
    {
        if (!def.ReqDtoType.IsValueType) // native aot cannot instantiate value type generic binders
            _ = sp.GetService(Types.IRequestBinderOf1.MakeGenericType(def.ReqDtoType));

        PrecompileValidatableType(def.ReqDtoType, []);
        PrecompileComplexBindGraph(def.ReqDtoType);

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

    /// <summary>
    /// precompiles the per-property metadata <see cref="ComplexSourceBinder"/> caches lazily (field name, type kind flags, setter,
    /// value parser, <c>List&lt;T&gt;</c> factory) for the object graph rooted at a <c>[FromForm]</c> / <c>[FromQuery]</c> property.
    /// </summary>
    static void PrecompileComplexBindGraph(Type tRequest)
    {
        if (tRequest == Types.EmptyRequest)
            return;

        var visited = new HashSet<Type>();

        foreach (var prop in tRequest.BindableProps())
        {
            // discovered off the dto directly rather than off RequestBinder<T>'s caches, since a user supplied binder
            // registration means that static ctor never runs during warmup.
            if (prop.IsDefined(Types.FromFormAttribute) || prop.IsDefined(Types.FromQueryAttribute))
                PrecompileComplexBindNode(prop.PropertyType, visited);
        }
    }

    static void PrecompileComplexBindNode(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
            return;

        // the binder news up every node it descends into. skip nodes it couldn't instantiate anyway (abstract /
        // ctor-less declared types) instead of throwing at startup for a graph that may never receive data.
        if (IsInstantiable(type))
            _ = type.ObjectFactory();

        foreach (var prop in type.BindableProps())
        {
            var meta = type.ComplexBindMeta(prop);

            if (meta.IsFormFile || meta.IsFormFileCollection)
                continue; // bound straight off the form collection - no nested graph, and IFormFile is never instantiated

            if (meta.IsCollection)
            {
                // nested collections are rejected by the binder rather than descended into - nothing to warm there
                if (meta is { ElementIsComplex: true, ElementIsCollection: false, ElementType: { } tElement })
                    PrecompileComplexBindNode(tElement, visited);
            }
            else if (meta.IsComplex)
                PrecompileComplexBindNode(meta.UnderlyingType!, visited);
        }
    }

    /// <summary>
    /// mirrors the constructibility rule in <c>BinderExtensions.ObjectFactory()</c>'s compile step - keep the two in sync,
    /// otherwise warmup starts throwing at startup for graphs the binder only fails on when data arrives.
    /// </summary>
    [UnconditionalSuppressMessage("aot", "IL2070")]
    static bool IsInstantiable(Type type)
        => !type.IsAbstract &&
           (type.IsValueType ||
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length > 0);
}
