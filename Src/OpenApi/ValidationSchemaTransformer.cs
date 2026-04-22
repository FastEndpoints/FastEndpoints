// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class ValidationSchemaTransformer : IOpenApiSchemaTransformer
{
    const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;
    static readonly ConditionalWeakTable<EndpointData, ValidatorBindingCacheEntry> _validatorBindingCache = new();
    static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();
    static readonly FluentValidationRule[] _rules = CreateDefaultRules();

    sealed class ValidatorBindingCacheEntry
    {
        public required Dictionary<Type, ValidatorBinding[]> Bindings { get; init; }
        public int NoValidatorsLogged;
    }

    sealed class ValidatorBinding
    {
        public required Type ValidatorType { get; init; }
        public string? PropertyPrefix { get; init; }
    }

    IServiceResolver? _serviceResolver;
    ILogger<ValidationSchemaTransformer>? _logger;
    Dictionary<Type, ValidatorBinding[]>? _validatorBindings;
    readonly object _initializeLock = new();
    volatile bool _initialized;

    void Initialize(IServiceProvider services)
    {
        if (_initialized)
            return;

        lock (_initializeLock)
        {
            if (_initialized)
                return;

            _serviceResolver = services.GetRequiredService<IServiceResolver>();
            _logger = services.GetRequiredService<ILogger<ValidationSchemaTransformer>>();
            var endpointData = _serviceResolver.Resolve<EndpointData>();
            var bindingCache = _validatorBindingCache.GetValue(
                endpointData,
                static data => new()
                {
                    Bindings = BuildValidatorBindings(data)
                });

            _validatorBindings = bindingCache.Bindings;

            if (_validatorBindings.Count == 0 && Interlocked.Exchange(ref bindingCache.NoValidatorsLogged, 1) == 0)
                _logger.NoValidatorsFound();

            _initialized = true;
        }
    }

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Initialize(context.ApplicationServices);

        if (_serviceResolver is null || _validatorBindings is not { Count: > 0 })
            return Task.CompletedTask;

        var tRequest = context.JsonTypeInfo.Type;

        if (!_validatorBindings.TryGetValue(tRequest, out var bindings) || bindings.Length == 0)
            return Task.CompletedTask;

        using var scope = _serviceResolver.CreateScope();

        foreach (var binding in bindings)
        {
            try
            {
                var validator = _serviceResolver.CreateInstance(binding.ValidatorType, scope.ServiceProvider) ??
                                throw new InvalidOperationException($"Unable to instantiate validator {binding.ValidatorType.Name}!");
                ApplyValidator(schema, (IValidator)validator, binding.PropertyPrefix ?? string.Empty, scope.ServiceProvider, []);
            }
            catch (Exception ex)
            {
                _logger?.ExceptionProcessingValidator(ex, binding.ValidatorType.Name);
            }
        }

        return Task.CompletedTask;
    }

    void ApplyValidator(OpenApiSchema schema,
                        IValidator validator,
                        string propertyPrefix,
                        IServiceProvider services,
                        HashSet<Type> activeChildValidators)
    {
        var rulesDict = validator.GetDictionaryOfRules(GetValidatorTargetType(validator));
        ApplyRulesToSchema(schema, rulesDict, propertyPrefix, services, activeChildValidators);
        ApplyRulesFromIncludedValidators(schema, validator, services, activeChildValidators);
    }

    void ApplyRulesToSchema(OpenApiSchema? schema,
                            ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                            string propertyPrefix,
                            IServiceProvider services,
                            HashSet<Type> activeChildValidators)
    {
        if (schema is null)
            return;

        if (schema.Properties is not null)
        {
            foreach (var schemaProperty in schema.Properties.Keys.ToArray())
                TryApplyValidation(schema, rulesDict, schemaProperty, propertyPrefix, services, activeChildValidators);
        }

        // recurse into polymorphism/union composites: allOf (inheritance), oneOf/anyOf (derived/union)
        RecurseComposite(schema.AllOf, rulesDict, propertyPrefix, services, activeChildValidators);
        RecurseComposite(schema.OneOf, rulesDict, propertyPrefix, services, activeChildValidators);
        RecurseComposite(schema.AnyOf, rulesDict, propertyPrefix, services, activeChildValidators);
    }

    void RecurseComposite(IList<IOpenApiSchema>? composite,
                          ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                          string propertyPrefix,
                          IServiceProvider services,
                          HashSet<Type> activeChildValidators)
    {
        if (composite is null)
            return;

        foreach (var entry in composite)
        {
            var resolved = entry.ResolveSchema();

            if (resolved is not null && resolved.Properties is { Count: > 0 })
                ApplyRulesToSchema(resolved, rulesDict, propertyPrefix, services, activeChildValidators);
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
    void ApplyRulesFromIncludedValidators(OpenApiSchema schema,
                                          IValidator validator,
                                          IServiceProvider services,
                                          HashSet<Type> activeChildValidators)
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
            var adapterMethod = adapter.GetType().GetMethod("GetValidator");

            if (adapterMethod == null)
                continue;

            var validationContext = Activator.CreateInstance(adapterMethod.GetParameters().First().ParameterType, [null!]);

            if (adapterMethod.Invoke(adapter, [validationContext, null!]) is not IValidator includeValidator)
                break;

            ApplyRulesToSchema(schema, includeValidator.GetDictionaryOfRules(GetValidatorTargetType(includeValidator)), string.Empty, services, activeChildValidators);
            ApplyRulesFromIncludedValidators(schema, includeValidator, services, activeChildValidators);
        }
    }

    void TryApplyValidation(OpenApiSchema schema,
                            ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                            string propertyName,
                            string parameterPrefix,
                            IServiceProvider services,
                            HashSet<Type> activeChildValidators)
    {
        var fullPropertyName = $"{parameterPrefix}{propertyName}";

        if (rulesDict.TryGetValue(fullPropertyName, out var validationRules))
        {
            foreach (var validationRule in validationRules)
                ApplyValidationRule(schema, validationRule, propertyName, services, activeChildValidators);
        }

        if (schema.Properties?.TryGetValue(propertyName, out var property) != true)
            return;

        var propertySchema = property.ResolveSchema();

        if (propertySchema is not null &&
            propertySchema.Properties is { Count: > 0 } &&
            propertySchema != schema)
            ApplyRulesToSchema(propertySchema, rulesDict, $"{fullPropertyName}.", services, activeChildValidators);
    }

    void ApplyValidationRule(OpenApiSchema schema,
                             IValidationRule validationRule,
                             string propertyName,
                             IServiceProvider services,
                             HashSet<Type> activeChildValidators)
    {
        foreach (var ruleComponent in validationRule.Components)
        {
            var propertyValidator = ruleComponent.Validator;

            if (propertyValidator.Name == "ChildValidatorAdaptor")
            {
                if (propertyValidator.GetType().Name.StartsWith("PolymorphicValidator"))
                {
                    _logger?.SwaggerWithFluentValidationIntegrationForPolymorphicValidatorsIsNotSupported(propertyValidator.GetType().Name);

                    continue;
                }

                var validatorTypeObj = propertyValidator.GetType().GetProperty("ValidatorType")?.GetValue(propertyValidator);

                if (validatorTypeObj is not Type validatorType)
                    throw new InvalidOperationException("ChildValidatorAdaptor.ValidatorType is null");

                if (validatorType.IsInterface || !activeChildValidators.Add(validatorType))
                {
                    continue;
                }

                try
                {
                    var childValidator = (IValidator)_serviceResolver!.CreateInstance(validatorType, services);

                    if (schema.Properties?.TryGetValue(propertyName, out var childPropSchema) == true)
                    {
                        var childSchema = childPropSchema.ResolveSchema();

                        if (childSchema is not null)
                        {
                            // check if array (RuleForEach)
                            if (childSchema.Type.HasValue &&
                                childSchema.Type.Value.HasFlag(JsonSchemaType.Array) &&
                                childSchema.Items.ResolveSchema() is { } itemsSchema)
                                ApplyValidator(itemsSchema, childValidator, string.Empty, services, activeChildValidators);
                            else
                                ApplyValidator(childSchema, childValidator, string.Empty, services, activeChildValidators);
                        }
                    }
                }
                finally
                {
                    activeChildValidators.Remove(validatorType);
                }

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

    static Dictionary<Type, ValidatorBinding[]> BuildValidatorBindings(EndpointData endpointData)
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

                var prefix = PropertyNameResolver.GetSchemaPropertyName(prop);
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

    static PropertyInfo[] GetPublicInstanceProperties(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return _propertyCache.GetOrAdd(type, static t => t.GetProperties(PublicInstance));
    }

    static Type GetValidatorTargetType(IValidator validator)
        => validator.GetType().GetGenericArgumentsOfType(Types.ValidatorOf1)?[0] ?? validator.GetType();

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

    static FluentValidationRule[] CreateDefaultRules()
        =>
        [
            new("Required")
            {
                Matches = propertyValidator => propertyValidator is INotNullValidator or INotEmptyValidator,
                Apply = context =>
                        {
                            var schema = context.Schema;
                            schema.Required ??= new HashSet<string>();
                            if (!schema.Required.Contains(context.PropertyKey) && !context.HasCondition)
                                schema.Required.Add(context.PropertyKey);
                        }
            },
            new("NotNull")
            {
                Matches = propertyValidator => propertyValidator is INotNullValidator,
                Apply = context =>
                        {
                            if (context.HasCondition)
                                return;

                            var schema = context.Schema;
                            if (schema.Properties?.TryGetValue(context.PropertyKey, out var p) == true &&
                                p is OpenApiSchema { Type: not null } prop &&
                                prop.Type.Value.HasFlag(JsonSchemaType.Null))
                                prop.Type = prop.Type.Value & ~JsonSchemaType.Null;
                        }
            },
            new("NotEmpty")
            {
                Matches = propertyValidator => propertyValidator is INotEmptyValidator,
                Apply = context =>
                        {
                            if (context.HasCondition)
                                return;

                            if (!context.TryGetPropertySchema(out var prop))
                                return;

                            if (prop.Type.HasValue && prop.Type.Value.HasFlag(JsonSchemaType.Null))
                                prop.Type = prop.Type.Value & ~JsonSchemaType.Null;

                            if (prop.Type == JsonSchemaType.Array)
                                prop.MinItems = 1;
                            else
                                prop.MinLength = 1;
                        }
            },
            new("Length")
            {
                Matches = propertyValidator => propertyValidator is ILengthValidator,
                Apply = context =>
                        {
                            if (!context.TryGetPropertySchema(out var prop))
                                return;

                            var lengthValidator = (ILengthValidator)context.PropertyValidator;
                            var target = prop;

                            if (target.Type == JsonSchemaType.Array)
                            {
                                if (lengthValidator.Max > 0)
                                    target.MaxItems = lengthValidator.Max;
                                if (lengthValidator.GetType() == typeof(MinimumLengthValidator<>) ||
                                    lengthValidator.GetType() == typeof(ExactLengthValidator<>) ||
                                    target.MinItems is null or 1)
                                    target.MinItems = lengthValidator.Min;

                                return;
                            }

                            if (lengthValidator.Max > 0)
                                target.MaxLength = lengthValidator.Max;
                            if (lengthValidator.GetType() == typeof(MinimumLengthValidator<>) ||
                                lengthValidator.GetType() == typeof(ExactLengthValidator<>) ||
                                target.MinLength is null or 1)
                                target.MinLength = lengthValidator.Min;
                        }
            },
            new("Pattern")
            {
                Matches = propertyValidator => propertyValidator is IRegularExpressionValidator,
                Apply = context =>
                        {
                            if (!context.TryGetPropertySchema(out var prop))
                                return;

                            var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
                            prop.Pattern = regularExpressionValidator.Expression;
                        }
            },
            new("Comparison")
            {
                Matches = propertyValidator => propertyValidator is IComparisonValidator,
                Apply = context =>
                        {
                            var comparisonValidator = (IComparisonValidator)context.PropertyValidator;

                            if (comparisonValidator.ValueToCompare.IsNumeric())
                            {
                                var valueToCompare = Convert.ToDecimal(comparisonValidator.ValueToCompare);
                                var valueStr = valueToCompare.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                if (!context.TryGetPropertySchema(out var prop))
                                    return;

                                switch (comparisonValidator.Comparison)
                                {
                                    case Comparison.GreaterThanOrEqual:
                                        prop.Minimum = valueStr;

                                        break;
                                    case Comparison.GreaterThan:
                                        prop.ExclusiveMinimum = valueStr;

                                        break;
                                    case Comparison.LessThanOrEqual:
                                        prop.Maximum = valueStr;

                                        break;
                                    case Comparison.LessThan:
                                        prop.ExclusiveMaximum = valueStr;

                                        break;
                                }
                            }
                        }
            },
            new("Between")
            {
                Matches = propertyValidator => propertyValidator is IBetweenValidator,
                Apply = context =>
                        {
                            var betweenValidator = (IBetweenValidator)context.PropertyValidator;

                            if (!context.TryGetPropertySchema(out var prop))
                                return;

                            if (betweenValidator.From.IsNumeric())
                            {
                                var fromStr = Convert.ToDecimal(betweenValidator.From).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                if (betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>)))
                                    prop.ExclusiveMinimum = fromStr;
                                else
                                    prop.Minimum = fromStr;
                            }

                            if (betweenValidator.To.IsNumeric())
                            {
                                var toStr = Convert.ToDecimal(betweenValidator.To).ToString(System.Globalization.CultureInfo.InvariantCulture);
                                if (betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>)))
                                    prop.ExclusiveMaximum = toStr;
                                else
                                    prop.Maximum = toStr;
                            }
                        }
            },
            new("AspNetCoreCompatibleEmail")
            {
                Matches = propertyValidator => propertyValidator.GetType().IsSubClassOfGeneric(typeof(AspNetCoreCompatibleEmailValidator<>)),
                Apply = context =>
                        {
                            if (!context.TryGetPropertySchema(out var prop))
                                return;

                            prop.Format = "email";
                            prop.Pattern = "^[^@]+@[^@]+$";
                        }
            }
        ];
}
