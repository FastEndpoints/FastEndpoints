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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class ValidationSchemaTransformer
{
    const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    sealed record CachedValidatorRules(ReadOnlyDictionary<string, List<IValidationRule>> Rules, CachedValidatorRules[] IncludedRules);

    static OpenApiSchema? ResolveForMutation(IOpenApiSchema? schema, bool localizeReferencedSchemas, Action<OpenApiSchema> replace)
    {
        if (!localizeReferencedSchemas || schema is not OpenApiSchemaReference)
            return schema.ResolveSchema();

        var cloned = schema.CloneAsConcreteSchema();

        if (cloned is not null)
            replace(cloned);

        return cloned;
    }

    sealed class ValidationSchemaApplier : IDisposable
    {
        readonly SharedContext _sharedCtx;
        readonly ILogger<ValidationSchemaTransformer>? _logger;
        readonly FluentValidationRule[] _rules;
        readonly ChildValidatorResolver _childResolver;
        readonly bool _localizeReferencedSchemas;
        readonly bool _usePropertyNamingPolicy;

        public ValidationSchemaApplier(SharedContext sharedCtx,
                                       IServiceResolver serviceResolver,
                                       ILogger<ValidationSchemaTransformer>? logger,
                                       Func<IServiceScope> createScope,
                                       FluentValidationRule[] rules,
                                       bool usePropertyNamingPolicy,
                                       bool localizeReferencedSchemas = false)
        {
            _sharedCtx = sharedCtx;
            _logger = logger;
            _rules = rules;
            _usePropertyNamingPolicy = usePropertyNamingPolicy;
            _localizeReferencedSchemas = localizeReferencedSchemas;
            _childResolver = new(
                serviceResolver,
                logger,
                createScope,
                ApplyValidator,
                ApplyRulesToSchema,
                usePropertyNamingPolicy,
                localizeReferencedSchemas);
        }

        public void Dispose()
            => _childResolver.Dispose();

        public void ApplyValidatorRules(OpenApiSchema schema, CachedValidatorRules cachedRules, string propertyPrefix, HashSet<Type> activeChildValidators)
        {
            ApplyRulesToSchema(schema, cachedRules.Rules, propertyPrefix, activeChildValidators);

            foreach (var includedRules in cachedRules.IncludedRules)
                ApplyValidatorRules(schema, includedRules, propertyPrefix, activeChildValidators);
        }

        void ApplyValidator(OpenApiSchema schema, IValidator validator, string propertyPrefix, HashSet<Type> activeChildValidators)
        {
            var rulesDict = validator.GetDictionaryOfRules(_sharedCtx.NamingPolicy, _usePropertyNamingPolicy, GetValidatorTargetType(validator));
            ApplyRulesToSchema(schema, rulesDict, propertyPrefix, activeChildValidators);
            _childResolver.ApplyRulesFromIncludedValidators(schema, validator, propertyPrefix, activeChildValidators, _sharedCtx.NamingPolicy, []);
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
        public static IEnumerable<IValidator> GetIncludedValidators(IValidator validator, ILogger<ValidationSchemaTransformer>? logger)
            => ChildValidatorResolver.GetIncludedValidators(validator, logger);

        void ApplyRulesToSchema(OpenApiSchema? schema, ReadOnlyDictionary<string, List<IValidationRule>> rulesDict, string propertyPrefix, HashSet<Type> activeChildValidators)
        {
            if (schema is null)
                return;

            TryApplyRootValidation(schema, rulesDict, propertyPrefix, activeChildValidators);

            if (schema.Properties is not null)
            {
                foreach (var (propertyName, propertySchema) in schema.Properties.ToArray())
                    TryApplyValidation(schema, rulesDict, propertyName, propertySchema, propertyPrefix, activeChildValidators);
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

            for (var i = 0; i < composite.Count; i++)
            {
                var resolved = ResolveForMutation(composite[i], _localizeReferencedSchemas, cloned => composite[i] = cloned);

                if (resolved?.Properties is { Count: > 0 })
                    ApplyRulesToSchema(resolved, rulesDict, propertyPrefix, activeChildValidators);
            }
        }

        void TryApplyValidation(OpenApiSchema schema,
                                ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                                string propertyName,
                                IOpenApiSchema? property,
                                string parameterPrefix,
                                HashSet<Type> activeChildValidators)
        {
            var fullPropertyName = $"{parameterPrefix}{propertyName}";

            if (rulesDict.TryGetValue(fullPropertyName, out var validationRules))
            {
                foreach (var validationRule in validationRules)
                    ApplyValidationRule(schema, validationRule, propertyName, activeChildValidators);
            }

            if (property is null)
                return;

            var propertySchema = ResolveForMutation(property, _localizeReferencedSchemas, cloned => schema.Properties![propertyName] = cloned);

            if (propertySchema is not null &&
                propertySchema.Properties is { Count: > 0 } &&
                propertySchema != schema)
            {
                ApplyRulesToSchema(propertySchema, rulesDict, $"{fullPropertyName}.", activeChildValidators);

                return;
            }

            if (propertySchema is { Type: { } type } &&
                type.HasFlag(JsonSchemaType.Array) &&
                ResolveForMutation(propertySchema.Items, _localizeReferencedSchemas, cloned => propertySchema.Items = cloned) is { Properties.Count: > 0 } itemsSchema)
                ApplyRulesToSchema(itemsSchema, rulesDict, $"{fullPropertyName}[].", activeChildValidators);
        }

        void TryApplyRootValidation(OpenApiSchema schema,
                                    ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                                    string propertyPrefix,
                                    HashSet<Type> activeChildValidators)
        {
            if (propertyPrefix.Length == 0 ||
                !rulesDict.TryGetValue(propertyPrefix.TrimEnd('.'), out var validationRules))
                return;

            foreach (var validationRule in validationRules)
            {
                foreach (var ruleComponent in validationRule.Components)
                {
                    var propertyValidator = ruleComponent.Validator;

                    if (propertyValidator.Name == "ChildValidatorAdaptor")
                        _childResolver.ApplyChildValidator(schema, propertyValidator, activeChildValidators);
                }
            }
        }

        void ApplyValidationRule(OpenApiSchema schema, IValidationRule validationRule, string propertyName, HashSet<Type> activeChildValidators)
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
                                        Func<IServiceScope> createScope,
                                        Action<OpenApiSchema, IValidator, string, HashSet<Type>> applyValidator,
                                        Action<OpenApiSchema?, ReadOnlyDictionary<string, List<IValidationRule>>, string, HashSet<Type>> applyRulesToSchema,
                                        bool usePropertyNamingPolicy,
                                        bool localizeReferencedSchemas) : IDisposable
    {
        IServiceScope? _scope;

        IServiceProvider Services => (_scope ??= createScope()).ServiceProvider;

        public void Dispose()
            => _scope?.Dispose();

        public static IEnumerable<IValidator> GetIncludedValidators(IValidator validator, ILogger<ValidationSchemaTransformer>? logger)
        {
            if (validator is not IEnumerable<IValidationRule> rules)
                yield break;

            foreach (var rule in rules)
            {
                if (!rule.HasNoCondition() || rule is not IIncludeRule)
                    continue;

                foreach (var component in rule.Components)
                {
                    var validatorType = component.Validator.GetType();

                    if (!validatorType.IsGenericType || validatorType.GetGenericTypeDefinition() != typeof(ChildValidatorAdaptor<,>))
                        continue;

                    if (TryGetIncludedValidator(component.Validator, logger, out var includeValidator))
                        yield return includeValidator;
                }
            }
        }

        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
        public void ApplyRulesFromIncludedValidators(OpenApiSchema schema,
                                                     IValidator validator,
                                                     string propertyPrefix,
                                                     HashSet<Type> activeChildValidators,
                                                     System.Text.Json.JsonNamingPolicy? namingPolicy,
                                                     HashSet<Type> activeIncludedValidators)
        {
            var validatorType = validator.GetType();

            if (!activeIncludedValidators.Add(validatorType))
                return;

            try
            {
                foreach (var includeValidator in GetIncludedValidators(validator, logger))
                {
                    var includeValidatorType = includeValidator.GetType();

                    if (activeIncludedValidators.Contains(includeValidatorType))
                        continue;

                    var rulesDict = includeValidator.GetDictionaryOfRules(namingPolicy, usePropertyNamingPolicy, GetValidatorTargetType(includeValidator));
                    applyRulesToSchema(schema, rulesDict, propertyPrefix, activeChildValidators);
                    ApplyRulesFromIncludedValidators(schema, includeValidator, propertyPrefix, activeChildValidators, namingPolicy, activeIncludedValidators);
                }
            }
            finally
            {
                activeIncludedValidators.Remove(validatorType);
            }
        }

        static bool TryGetIncludedValidator(IPropertyValidator adapter, ILogger<ValidationSchemaTransformer>? logger, [NotNullWhen(true)] out IValidator? validator)
        {
            validator = null;
            var adapterType = adapter.GetType();

            try
            {
                var accessor = ChildValidatorAdapterAccessor.Get(adapterType);

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
                logger?.ExceptionProcessingValidator(ex, adapterType.Name);

                return false;
            }
        }

        public void ApplyChildValidator(OpenApiSchema schema, string propertyName, IPropertyValidator propertyValidator, HashSet<Type> activeChildValidators)
        {
            if (!TryCreateChildValidator(propertyValidator, activeChildValidators, out var validatorType, out var childValidator))
                return;

            try
            {
                if (schema.Properties?.TryGetValue(propertyName, out var childPropSchema) == true)
                {
                    var childSchema = ResolveForMutation(childPropSchema, localizeReferencedSchemas, cloned => schema.Properties![propertyName] = cloned);

                    if (childSchema is not null)
                    {
                        if (childSchema.Type.HasValue &&
                            childSchema.Type.Value.HasFlag(JsonSchemaType.Array) &&
                            ResolveForMutation(childSchema.Items, localizeReferencedSchemas, cloned => childSchema.Items = cloned) is { } itemsSchema)
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

        public void ApplyChildValidator(OpenApiSchema schema, IPropertyValidator propertyValidator, HashSet<Type> activeChildValidators)
        {
            if (TryCreateChildValidator(propertyValidator, activeChildValidators, out var validatorType, out var childValidator))
            {
                try
                {
                    applyValidator(schema, childValidator, string.Empty, activeChildValidators);
                }
                finally
                {
                    activeChildValidators.Remove(validatorType);
                }
            }
        }

        bool TryCreateChildValidator(IPropertyValidator propertyValidator,
                                     HashSet<Type> activeChildValidators,
                                     [NotNullWhen(true)] out Type? validatorType,
                                     [NotNullWhen(true)] out IValidator? childValidator)
        {
            validatorType = null;
            childValidator = null;

            var propertyValidatorType = propertyValidator.GetType();

            if (propertyValidatorType.Name.StartsWith("PolymorphicValidator"))
            {
                logger?.SwaggerWithFluentValidationIntegrationForPolymorphicValidatorsIsNotSupported(propertyValidatorType.Name);

                return false;
            }

            var validatorTypeObj = propertyValidatorType.GetProperty("ValidatorType")?.GetValue(propertyValidator);

            if (validatorTypeObj is not Type resolvedValidatorType)
                throw new InvalidOperationException("ChildValidatorAdaptor.ValidatorType is null");

            if (resolvedValidatorType.IsInterface || !activeChildValidators.Add(resolvedValidatorType))
                return false;

            validatorType = resolvedValidatorType;
            childValidator = (IValidator)serviceResolver.CreateInstance(resolvedValidatorType, Services);

            return true;
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

    static Type GetValidatorTargetType(IValidator validator)
        => validator.GetType().GetGenericArgumentsOfType(Types.ValidatorOf1)?[0] ?? validator.GetType();
}