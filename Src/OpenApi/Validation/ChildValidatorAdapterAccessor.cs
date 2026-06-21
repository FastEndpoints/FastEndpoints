using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace FastEndpoints.OpenApi;

sealed class ChildValidatorAdapterAccessor
{
    const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    static readonly ConcurrentDictionary<Type, ChildValidatorAdapterAccessor> _cache = new();

    public bool IsAvailable { get; private init; }
    public required Func<object?, object?> CreateValidationContext { get; init; }
    public required Func<object, object?, object?> GetValidator { get; init; }

    public static ChildValidatorAdapterAccessor Get(Type adapterType)
        => _cache.GetOrAdd(adapterType, Create);

    [UnconditionalSuppressMessage("aot", "IL2070"), UnconditionalSuppressMessage("aot", "IL2075")]
    static ChildValidatorAdapterAccessor Create(Type adapterType)
    {
        var getValidatorMethod = adapterType.GetMethod("GetValidator", PublicInstance);

        if (getValidatorMethod is null)
            return Missing();

        var parameters = getValidatorMethod.GetParameters();

        if (parameters.Length < 2)
            return Missing();

        var contextType = parameters[0].ParameterType;
        var contextCtor = contextType.GetConstructors(PublicInstance)
                                     .FirstOrDefault(c => c.GetParameters().Length == 1);

        if (contextCtor is null)
            return Missing();

        return new()
        {
            IsAvailable = true,
            CreateValidationContext = CompileContextFactory(contextCtor),
            GetValidator = CompileGetValidator(adapterType, getValidatorMethod, parameters)
        };
    }

    static ChildValidatorAdapterAccessor Missing()
        => new()
        {
            IsAvailable = false,
            CreateValidationContext = static _ => null,
            GetValidator = static (_, _) => null
        };

    static Func<object?, object?> CompileContextFactory(ConstructorInfo constructor)
    {
        var instanceParam = Expression.Parameter(typeof(object), "instance");
        var newContext = Expression.New(constructor, Expression.Convert(instanceParam, constructor.GetParameters()[0].ParameterType));
        var body = Expression.Convert(newContext, typeof(object));

        return Expression.Lambda<Func<object?, object?>>(body, instanceParam).Compile();
    }

    static Func<object, object?, object?> CompileGetValidator(Type adapterType, MethodInfo getValidatorMethod, ParameterInfo[] parameters)
    {
        var adapterParam = Expression.Parameter(typeof(object), "adapter");
        var contextParam = Expression.Parameter(typeof(object), "context");
        var typedAdapter = Expression.Convert(adapterParam, adapterType);
        var typedContext = Expression.Convert(contextParam, parameters[0].ParameterType);
        var typedValue = Expression.Constant(null, parameters[1].ParameterType);
        var call = Expression.Call(typedAdapter, getValidatorMethod, typedContext, typedValue);
        var body = Expression.Convert(call, typeof(object));

        return Expression.Lambda<Func<object, object?, object?>>(body, adapterParam, contextParam).Compile();
    }
}