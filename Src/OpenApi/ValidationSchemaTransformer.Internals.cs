using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class ValidationSchemaTransformer
{
    const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    sealed class ValidatorBinding
    {
        public required Type ValidatorType { get; init; }
        public string? PropertyPrefix { get; init; }
    }

    sealed class ValidatorBindingProvider
    {
        public static Dictionary<Type, ValidatorBinding[]> Build(EndpointData endpointData, System.Text.Json.JsonNamingPolicy? namingPolicy)
        {
            var bindings = new Dictionary<Type, List<ValidatorBinding>>();

            foreach (var validatorType in endpointData.Found
                                                    .Where(e => e.ValidatorType != null)
                                                    .Select(e => e.ValidatorType!)
                                                    .Distinct())
            {
                var validatorTargetType = validatorType.GetGenericArgumentsOfType(Types.ValidatorOf1)?[0];

                if (validatorTargetType is null)
                    continue;

                AddBinding(validatorTargetType, validatorType, null);

                if (!SupportsNestedPropertyWalk(validatorTargetType))
                    continue;

                foreach (var prop in GetPublicInstanceProperties(validatorTargetType))
                {
                    if (!SupportsNestedPropertyTarget(prop.PropertyType))
                        continue;

                    var prefix = PropertyNameResolver.GetSchemaPropertyName(prop, namingPolicy);
                    AddBinding(prop.PropertyType, validatorType, prefix + ".");
                }
            }

            return bindings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());

            void AddBinding(Type targetType, Type validatorType, string? propertyPrefix)
            {
                targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

                if (!bindings.TryGetValue(targetType, out var targetBindings))
                    bindings[targetType] = targetBindings = [];

                if (targetBindings.Any(b => b.ValidatorType == validatorType && b.PropertyPrefix == propertyPrefix))
                    return;

                targetBindings.Add(new() { ValidatorType = validatorType, PropertyPrefix = propertyPrefix });
            }
        }

        static bool SupportsNestedPropertyWalk(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type.IsPrimitive || type.IsEnum || type.IsArray || type.IsPointer)
                return false;

            if (type == typeof(string) ||
                type == typeof(decimal) ||
                type == typeof(DateTime) ||
                type == typeof(DateTimeOffset) ||
                type == typeof(DateOnly) ||
                type == typeof(TimeOnly) ||
                type == typeof(TimeSpan) ||
                type == typeof(Guid) ||
                type == typeof(Uri) ||
                type == typeof(byte[]) ||
                type == typeof(object) ||
                type == typeof(Version))
                return false;

            return !typeof(System.Collections.IEnumerable).IsAssignableFrom(type);
        }

        static bool SupportsNestedPropertyTarget(Type type)
            => SupportsNestedPropertyWalk(type);
    }

    sealed class ValidationSchemaApplier
    {
        readonly SharedContext _sharedCtx;
        readonly ILogger<ValidationSchemaTransformer>? _logger;
        readonly FluentValidationRule[] _rules;
        readonly ChildValidatorResolver _childResolver;

        public ValidationSchemaApplier(SharedContext sharedCtx,
                                       IServiceResolver serviceResolver,
                                       ILogger<ValidationSchemaTransformer>? logger,
                                       IServiceProvider services,
                                       FluentValidationRule[] rules)
        {
            _sharedCtx = sharedCtx;
            _logger = logger;
            _rules = rules;
            _childResolver = new(serviceResolver, logger, services, ApplyValidator, ApplyRulesToSchema);
        }

        public void ApplyValidator(OpenApiSchema schema, IValidator validator, string propertyPrefix, HashSet<Type> activeChildValidators)
        {
            var rulesDict = validator.GetDictionaryOfRules(_sharedCtx.NamingPolicy, GetValidatorTargetType(validator));
            ApplyRulesToSchema(schema, rulesDict, propertyPrefix, activeChildValidators);
            _childResolver.ApplyRulesFromIncludedValidators(schema, validator, activeChildValidators, _sharedCtx.NamingPolicy);
        }

        void ApplyRulesToSchema(OpenApiSchema? schema,
                                ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                                string propertyPrefix,
                                HashSet<Type> activeChildValidators)
        {
            if (schema is null)
                return;

            if (schema.Properties is not null)
            {
                foreach (var schemaProperty in schema.Properties.Keys.ToArray())
                    TryApplyValidation(schema, rulesDict, schemaProperty, propertyPrefix, activeChildValidators);
            }

            RecurseComposite(schema.AllOf, rulesDict, propertyPrefix, activeChildValidators);
            RecurseComposite(schema.OneOf, rulesDict, propertyPrefix, activeChildValidators);
            RecurseComposite(schema.AnyOf, rulesDict, propertyPrefix, activeChildValidators);
        }

        void RecurseComposite(IList<IOpenApiSchema>? composite,
                              ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                              string propertyPrefix,
                              HashSet<Type> activeChildValidators)
        {
            if (composite is null)
                return;

            foreach (var entry in composite)
            {
                var resolved = entry.ResolveSchema();

                if (resolved is not null && resolved.Properties is { Count: > 0 })
                    ApplyRulesToSchema(resolved, rulesDict, propertyPrefix, activeChildValidators);
            }
        }

        void TryApplyValidation(OpenApiSchema schema,
                                ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                                string propertyName,
                                string parameterPrefix,
                                HashSet<Type> activeChildValidators)
        {
            var fullPropertyName = $"{parameterPrefix}{propertyName}";

            if (rulesDict.TryGetValue(fullPropertyName, out var validationRules))
            {
                foreach (var validationRule in validationRules)
                    ApplyValidationRule(schema, validationRule, propertyName, activeChildValidators);
            }

            if (schema.Properties?.TryGetValue(propertyName, out var property) != true)
                return;

            var propertySchema = property.ResolveSchema();

            if (propertySchema is not null &&
                propertySchema.Properties is { Count: > 0 } &&
                propertySchema != schema)
                ApplyRulesToSchema(propertySchema, rulesDict, $"{fullPropertyName}.", activeChildValidators);
        }

        void ApplyValidationRule(OpenApiSchema schema,
                                 IValidationRule validationRule,
                                 string propertyName,
                                 HashSet<Type> activeChildValidators)
        {
            foreach (var ruleComponent in validationRule.Components)
            {
                var propertyValidator = ruleComponent.Validator;

                if (propertyValidator.Name == "ChildValidatorAdaptor")
                {
                    _childResolver.ApplyChildValidator(schema, propertyName, propertyValidator, activeChildValidators);
                    continue;
                }

                foreach (var rule in _rules)
                {
                    if (!rule.Matches(propertyValidator))
                        continue;

                    try
                    {
                        rule.Apply(new(schema, propertyName, propertyValidator, ruleComponent.HasCondition()));
                    }
                    catch (Exception ex)
                    {
                        _logger?.FailedToApplyValidationRule(ex, propertyName, propertyValidator.Name);
                    }
                }
            }
        }
    }

    sealed class ChildValidatorResolver(IServiceResolver serviceResolver,
                                        ILogger<ValidationSchemaTransformer>? logger,
                                        IServiceProvider services,
                                        Action<OpenApiSchema, IValidator, string, HashSet<Type>> applyValidator,
                                        Action<OpenApiSchema?, ReadOnlyDictionary<string, List<IValidationRule>>, string, HashSet<Type>> applyRulesToSchema)
    {
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
        public void ApplyRulesFromIncludedValidators(OpenApiSchema schema,
                                                     IValidator validator,
                                                     HashSet<Type> activeChildValidators,
                                                     System.Text.Json.JsonNamingPolicy? namingPolicy)
        {
            if (validator is not IEnumerable<IValidationRule> rules)
                return;

            var childAdapters = rules
                                .Where(rule => rule.HasNoCondition() && rule is IIncludeRule)
                                .SelectMany(includeRule => includeRule.Components.Select(c => c.Validator))
                                .Where(
                                    v => v.GetType() is { IsGenericType: true } vType &&
                                         vType.GetGenericTypeDefinition() == typeof(ChildValidatorAdaptor<,>))
                                .ToList();

            foreach (var adapter in childAdapters)
            {
                if (!TryGetIncludedValidator(adapter, out var includeValidator))
                    continue;

                var rulesDict = includeValidator.GetDictionaryOfRules(namingPolicy, GetValidatorTargetType(includeValidator));
                applyRulesToSchema(schema, rulesDict, string.Empty, activeChildValidators);
                ApplyRulesFromIncludedValidators(schema, includeValidator, activeChildValidators, namingPolicy);
            }
        }

        bool TryGetIncludedValidator(IPropertyValidator adapter, [NotNullWhen(true)] out IValidator? validator)
        {
            validator = null;

            try
            {
                var accessor = ChildValidatorAdapterAccessor.Get(adapter.GetType());

                if (!accessor.IsAvailable)
                    return false;

                var validationContext = accessor.CreateValidationContext(null);

                if (accessor.GetValidator(adapter, validationContext) is not IValidator resolvedValidator)
                    return false;

                validator = resolvedValidator;

                return true;
            }
            catch (Exception ex)
            {
                logger?.ExceptionProcessingValidator(ex, adapter.GetType().Name);

                return false;
            }
        }

        public void ApplyChildValidator(OpenApiSchema schema,
                                        string propertyName,
                                        IPropertyValidator propertyValidator,
                                        HashSet<Type> activeChildValidators)
        {
            if (propertyValidator.GetType().Name.StartsWith("PolymorphicValidator"))
            {
                logger?.SwaggerWithFluentValidationIntegrationForPolymorphicValidatorsIsNotSupported(propertyValidator.GetType().Name);

                return;
            }

            var validatorTypeObj = propertyValidator.GetType().GetProperty("ValidatorType")?.GetValue(propertyValidator);

            if (validatorTypeObj is not Type validatorType)
                throw new InvalidOperationException("ChildValidatorAdaptor.ValidatorType is null");

            if (validatorType.IsInterface || !activeChildValidators.Add(validatorType))
                return;

            try
            {
                var childValidator = (IValidator)serviceResolver.CreateInstance(validatorType, services);

                if (schema.Properties?.TryGetValue(propertyName, out var childPropSchema) == true)
                {
                    var childSchema = childPropSchema.ResolveSchema();

                    if (childSchema is not null)
                    {
                        if (childSchema.Type.HasValue &&
                            childSchema.Type.Value.HasFlag(JsonSchemaType.Array) &&
                            childSchema.Items.ResolveSchema() is { } itemsSchema)
                            applyValidator(itemsSchema, childValidator, string.Empty, activeChildValidators);
                        else
                            applyValidator(childSchema, childValidator, string.Empty, activeChildValidators);
                    }
                }
            }
            finally
            {
                activeChildValidators.Remove(validatorType);
            }
        }

    }

    sealed class ChildValidatorAdapterAccessor
    {
        static readonly ConcurrentDictionary<Type, ChildValidatorAdapterAccessor> _cache = new();

        public bool IsAvailable { get; private init; }
        public required Func<object?, object?> CreateValidationContext { get; init; }
        public required Func<object, object?, object?> GetValidator { get; init; }

        public static ChildValidatorAdapterAccessor Get(Type adapterType)
            => _cache.GetOrAdd(adapterType, Create);

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

    static PropertyInfo[] GetPublicInstanceProperties(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return _propertyCache.GetOrAdd(type, static t => t.GetProperties(PublicInstance));
    }

    static Type GetValidatorTargetType(IValidator validator)
        => validator.GetType().GetGenericArgumentsOfType(Types.ValidatorOf1)?[0] ?? validator.GetType();
}
