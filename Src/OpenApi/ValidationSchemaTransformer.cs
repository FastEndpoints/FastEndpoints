// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
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

    IServiceResolver? _serviceResolver;
    ILogger<ValidationSchemaTransformer>? _logger;
    Type[]? _validatorTypes;
    FluentValidationRule[]? _rules;
    readonly Dictionary<string, IValidator> _childAdaptorValidators = new();
    bool _initialized;

    void Initialize(IServiceProvider services)
    {
        if (_initialized)
            return;

        _initialized = true;

        _serviceResolver = services.GetRequiredService<IServiceResolver>();
        _logger = services.GetRequiredService<ILogger<ValidationSchemaTransformer>>();
        _rules = CreateDefaultRules();
        _validatorTypes = _serviceResolver.Resolve<EndpointData>().Found
                                          .Where(e => e.ValidatorType != null)
                                          .Select(e => e.ValidatorType!)
                                          .Distinct()
                                          .ToArray();

        if (_validatorTypes.Length == 0)
            _logger.NoValidatorsFound();
    }

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        Initialize(context.ApplicationServices);

        if (_serviceResolver is null || _validatorTypes is not { Length: > 0 } || _rules is null)
            return Task.CompletedTask;

        var tRequest = context.JsonTypeInfo.Type;
        using var scope = _serviceResolver.CreateScope();
        var namingPolicy = Extensions.NamingPolicy;
        var supportsNestedPropertyWalk = IsComplexType(tRequest);

        foreach (var tValidator in _validatorTypes)
        {
            try
            {
                var validatorTargetType = tValidator.BaseType?.GenericTypeArguments.FirstOrDefault();

                if (validatorTargetType == tRequest)
                {
                    var validator = _serviceResolver.CreateInstance(tValidator, scope.ServiceProvider) ??
                                    throw new InvalidOperationException($"Unable to instantiate validator {tValidator.Name}!");
                    ApplyValidator(schema, (IValidator)validator, "", scope.ServiceProvider);

                    break;
                }

                // handle nested properties: if the validator's target type has a property of the current schema type,
                // apply the validator's rules using the property name as prefix so that nested rules like
                // RuleFor(x => x.Product.Price) are applied to the Product schema.
                if (validatorTargetType is null || !supportsNestedPropertyWalk)
                    continue;

                foreach (var prop in validatorTargetType.GetProperties(PublicInstance))
                {
                    if (prop.PropertyType != tRequest)
                        continue;

                    var validator = _serviceResolver.CreateInstance(tValidator, scope.ServiceProvider) ??
                                    throw new InvalidOperationException($"Unable to instantiate validator {tValidator.Name}!");
                    var prefix = namingPolicy?.ConvertName(prop.Name) ?? prop.Name;
                    ApplyValidator(schema, (IValidator)validator, prefix + ".", scope.ServiceProvider);
                }
            }
            catch (Exception ex)
            {
                _logger?.ExceptionProcessingValidator(ex, tValidator.Name);
            }
        }

        return Task.CompletedTask;
    }

    void ApplyValidator(OpenApiSchema schema, IValidator validator, string propertyPrefix, IServiceProvider services)
    {
        var rulesDict = validator.GetDictionaryOfRules();
        ApplyRulesToSchema(schema, rulesDict, propertyPrefix, services);
        ApplyRulesFromIncludedValidators(schema, validator, services);
    }

    void ApplyRulesToSchema(OpenApiSchema? schema,
                            ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                            string propertyPrefix,
                            IServiceProvider services)
    {
        if (schema is null)
            return;

        if (schema.Properties is not null)
        {
            foreach (var schemaProperty in schema.Properties.Keys.ToArray())
                TryApplyValidation(schema, rulesDict, schemaProperty, propertyPrefix, services);
        }

        // recurse into polymorphism/union composites: allOf (inheritance), oneOf/anyOf (derived/union)
        RecurseComposite(schema.AllOf, rulesDict, propertyPrefix, services);
        RecurseComposite(schema.OneOf, rulesDict, propertyPrefix, services);
        RecurseComposite(schema.AnyOf, rulesDict, propertyPrefix, services);
    }

    void RecurseComposite(IList<IOpenApiSchema>? composite,
                          ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                          string propertyPrefix,
                          IServiceProvider services)
    {
        if (composite is null)
            return;

        foreach (var entry in composite)
        {
            var resolved = entry.ResolveSchema();

            if (resolved is not null && resolved.Properties is { Count: > 0 })
                ApplyRulesToSchema(resolved, rulesDict, propertyPrefix, services);
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
    void ApplyRulesFromIncludedValidators(OpenApiSchema schema, IValidator validator, IServiceProvider services)
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

            ApplyRulesToSchema(schema, includeValidator.GetDictionaryOfRules(), string.Empty, services);
            ApplyRulesFromIncludedValidators(schema, includeValidator, services);
        }
    }

    void TryApplyValidation(OpenApiSchema schema,
                            ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                            string propertyName,
                            string parameterPrefix,
                            IServiceProvider services)
    {
        var fullPropertyName = $"{parameterPrefix}{propertyName}";

        if (rulesDict.TryGetValue(fullPropertyName, out var validationRules))
        {
            foreach (var validationRule in validationRules)
                ApplyValidationRule(schema, validationRule, propertyName, services);
        }

        if (schema.Properties?.TryGetValue(propertyName, out var property) != true)
            return;

        var propertySchema = property.ResolveSchema();

        if (propertySchema is not null &&
            propertySchema.Properties is { Count: > 0 } &&
            propertySchema != schema)
            ApplyRulesToSchema(propertySchema, rulesDict, $"{fullPropertyName}.", services);
    }

    void ApplyValidationRule(OpenApiSchema schema, IValidationRule validationRule, string propertyName, IServiceProvider services)
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

                if (!validatorType.IsInterface &&
                    !_childAdaptorValidators.TryGetValue(validatorType.FullName!, out var childValidator))
                {
                    childValidator = _childAdaptorValidators[validatorType.FullName!] =
                                         (IValidator)_serviceResolver!.CreateInstance(validatorType, services);
                }
                else
                {
                    // interface validators can't be instantiated; already-cached validators are skipped
                    // to prevent infinite recursion for circular validator references
                    continue;
                }

                if (schema.Properties?.TryGetValue(propertyName, out var childPropSchema) == true)
                {
                    var childSchema = childPropSchema.ResolveSchema();

                    if (childSchema is not null)
                    {
                        // check if array (RuleForEach)
                        if (childSchema.Type == JsonSchemaType.Array && childSchema.Items.ResolveSchema() is { } itemsSchema)
                            ApplyValidator(itemsSchema, childValidator, string.Empty, services);
                        else
                            ApplyValidator(childSchema, childValidator, string.Empty, services);
                    }
                }

                continue;
            }

            foreach (var rule in _rules!)
            {
                if (!rule.Matches(propertyValidator))
                    continue;

                try
                {
                    rule.Apply(new(schema, propertyName, propertyValidator, ruleComponent.HasCondition()));
                }
                catch
                {
                    //do nothing
                }
            }
        }
    }

    static bool IsComplexType(Type type)
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
            type == typeof(object))
            return false;

        // treat all collections/dictionaries as non-complex at the validator-target level; their
        // element/value types will be visited via property walks instead.
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type))
            return false;

        return true;
    }

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

                            var schema = context.Schema;

                            if (schema.Properties?.TryGetValue(context.PropertyKey, out var p) == true && p is OpenApiSchema prop)
                            {
                                if (prop.Type.HasValue && prop.Type.Value.HasFlag(JsonSchemaType.Null))
                                    prop.Type = prop.Type.Value & ~JsonSchemaType.Null;
                                prop.MinLength = 1;
                            }
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
                            if (lengthValidator.Max > 0)
                                prop.MaxLength = lengthValidator.Max;
                            if (lengthValidator.GetType() == typeof(MinimumLengthValidator<>) ||
                                lengthValidator.GetType() == typeof(ExactLengthValidator<>) ||
                                prop.MinLength is null or 1)
                                prop.MinLength = lengthValidator.Min;
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