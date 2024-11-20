// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//         of this software and associated documentation files (the "Software"), to deal
//         in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
//         furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
//         copies or substantial portions of the Software.
//
//         THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//         IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//                                                                 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//         AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//         LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.ObjectModel;
using FastEndpoints.Swagger.ValidationProcessor;
using FastEndpoints.Swagger.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using NJsonSchema.Generation;

// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace FastEndpoints.Swagger;

sealed class ValidationSchemaProcessor : ISchemaProcessor
{
    readonly IServiceResolver _serviceResolver;
    readonly ILogger<ValidationSchemaProcessor> _logger;
    static Type[]? _validatorTypes;
    readonly FluentValidationRule[] _rules;
    readonly Dictionary<string, IValidator> _childAdaptorValidators = new();

    public ValidationSchemaProcessor(IServiceResolver serviceResolver, ILogger<ValidationSchemaProcessor> logger)
    {
        _serviceResolver = serviceResolver;
        _logger = logger;
        _rules = CreateDefaultRules();
        _validatorTypes ??= _serviceResolver.Resolve<EndpointData>().Found
                                            .Where(e => e.ValidatorType != null)
                                            .Select(e => e.ValidatorType!)
                                            .Distinct()
                                            .ToArray();

        if (_validatorTypes?.Length is null or 0)
            _logger.LogInformation("No validators found in the system!");
    }

    public void Process(SchemaProcessorContext context)
    {
        if (_validatorTypes?.Length is null or 0)
            return;

        var tRequest = context.ContextualType;
        using var scope = _serviceResolver.CreateScope();

        foreach (var tValidator in _validatorTypes)
        {
            try
            {
                if (tValidator.BaseType?.GenericTypeArguments.FirstOrDefault() == tRequest)
                {
                    var validator = _serviceResolver.CreateInstance(tValidator, scope.ServiceProvider);

                    if (validator is null)
                        throw new InvalidOperationException("Unable to instantiate validator!");

                    ApplyValidator(context.Schema, (IValidator)validator, "", scope.ServiceProvider);

                    break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception while processing {@tValidator}", tValidator);
            }
        }
    }

    void ApplyValidator(JsonSchema schema, IValidator validator, string propertyPrefix, IServiceProvider services)
    {
        // Create dict of rules for this validator
        var rulesDict = validator.GetDictionaryOfRules();
        ApplyRulesToSchema(schema, rulesDict, propertyPrefix, services);
        ApplyRulesFromIncludedValidators(schema, validator, services);
    }

    void ApplyRulesToSchema(JsonSchema? schema,
                            ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                            string propertyPrefix,
                            IServiceProvider services)
    {
        if (schema is null)
            return;

        // Add properties from current schema/class
        if (schema.ActualProperties != null)
        {
            foreach (var schemaProperty in schema.ActualProperties.Keys)
                TryApplyValidation(schema, rulesDict, schemaProperty, propertyPrefix, services);
        }

        // Add properties from base class
        ApplyRulesToSchema(schema.InheritedSchema, rulesDict, propertyPrefix, services);
    }

    void ApplyRulesFromIncludedValidators(JsonSchema schema, IValidator validator, IServiceProvider services)
    {
        if (validator is not IEnumerable<IValidationRule> rules)
            return;

        // Note: IValidatorDescriptor doesn't return IncludeRules, so we need to get validators manually.
        var childAdapters = rules
                            .Where(rule => rule.HasNoCondition() && rule is IIncludeRule)
                            .SelectMany(includeRule => includeRule.Components.Select(c => c.Validator))
                            .Where(x => x.GetType().IsGenericType && x.GetType().GetGenericTypeDefinition() == typeof(ChildValidatorAdaptor<,>))
                            .ToList();

        foreach (var adapter in childAdapters)
        {
            var adapterMethod = adapter.GetType().GetMethod("GetValidator");

            if (adapterMethod == null)
                continue;

            // Create validation context of generic type
            // NOTE: do not use service resolver here to create the validation context: https://github.com/FastEndpoints/FastEndpoints/issues/827
            var validationContext = Activator.CreateInstance(adapterMethod.GetParameters().First().ParameterType, [null!]);

            if (adapterMethod.Invoke(adapter, [validationContext, null!]) is not IValidator includeValidator)
                break;

            ApplyRulesToSchema(schema, includeValidator.GetDictionaryOfRules(), string.Empty, services);
            ApplyRulesFromIncludedValidators(schema, includeValidator, services);
        }
    }

    void TryApplyValidation(JsonSchema schema,
                            ReadOnlyDictionary<string, List<IValidationRule>> rulesDict,
                            string propertyName,
                            string parameterPrefix,
                            IServiceProvider services)
    {
        // Build the full property name with composition route: request.child.property
        var fullPropertyName = $"{parameterPrefix}{propertyName}";

        // Try to get a list of valid rules that matches this property name
        if (rulesDict.TryGetValue(fullPropertyName, out var validationRules))
        {
            // Go through each rule and apply it to the schema
            foreach (var validationRule in validationRules)
                ApplyValidationRule(schema, validationRule, propertyName, services);
        }

        // If the property is a child object, recursively apply validation to it adding prefix as we go down one level
        var property = schema.ActualProperties[propertyName];
        var propertySchema = property.ActualSchema;
        if (propertySchema.ActualProperties is not null && propertySchema.ActualProperties.Count > 0 && propertySchema != schema)
            ApplyRulesToSchema(propertySchema, rulesDict, $"{fullPropertyName}.", services);
    }

    void ApplyValidationRule(JsonSchema schema, IValidationRule validationRule, string propertyName, IServiceProvider services)
    {
        foreach (var ruleComponent in validationRule.Components)
        {
            var propertyValidator = ruleComponent.Validator;

            // 1. If the propertyValidator is a ChildValidatorAdaptor we need to get the underlying validator
            // i.e. for RuleFor().SetValidator() or RuleForEach().SetValidator()
            if (propertyValidator.Name == "ChildValidatorAdaptor")
            {
                if (propertyValidator.GetType().Name.StartsWith("PolymorphicValidator"))
                {
                    //todo: figure out how to obtain the concrete instances of child validators from PolymorphicValidator<>

                    _logger?.LogWarning("Swagger+Fluentvalidation integration for polymorphic validators is not supported.");

                    continue;
                }

                // Get underlying validator using reflection
                var validatorTypeObj = propertyValidator.GetType().GetProperty("ValidatorType")?.GetValue(propertyValidator);

                // Check if something went wrong
                if (validatorTypeObj is not Type validatorType)
                    throw new InvalidOperationException("ChildValidatorAdaptor.ValidatorType is null");

                // Retrieve or create an instance of the validator
                if (!_childAdaptorValidators.TryGetValue(validatorType.FullName!, out var childValidator))
                {
                    childValidator = _childAdaptorValidators[validatorType.FullName!] =
                                         (IValidator)_serviceResolver.CreateInstance(validatorType, services);
                }

                // Apply the validator to the schema. Again, recursively
                var childSchema = schema.ActualProperties[propertyName].ActualSchema;

                // Check if it is an array (RuleForEach()). In this case we need to apply validator to an Item Schema
                childSchema = childSchema.Type == JsonObjectType.Array ? childSchema.Item!.ActualSchema : childSchema;
                ApplyValidator(childSchema, childValidator, string.Empty, services);

                continue;
            }

            // 2. Normal property validator processing
            foreach (var rule in _rules)
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

    static FluentValidationRule[] CreateDefaultRules()
        =>
        [
            new("Required")
            {
                Matches = propertyValidator => propertyValidator is INotNullValidator or INotEmptyValidator,
                Apply = context =>
                        {
                            var schema = context.Schema;
                            if (!schema.RequiredProperties.Contains(context.PropertyKey) && !context.HasCondition)
                                schema.RequiredProperties.Add(context.PropertyKey);
                        }
            },
            new("NotNull")
            {
                Matches = propertyValidator => propertyValidator is INotNullValidator,
                Apply = context =>
                        {
                            if (context.HasCondition)
                                return; //don't apply if this is a conditional rule

                            var schema = context.Schema;
                            var properties = schema.ActualProperties;
                            properties[context.PropertyKey].IsNullableRaw = false;
                            if (properties[context.PropertyKey].Type.HasFlag(JsonObjectType.Null))
                                properties[context.PropertyKey].Type &= ~JsonObjectType.Null; // Remove nullable
                            var oneOfsWithReference = properties[context.PropertyKey].OneOf
                                                                                     .Where(x => x.Reference != null)
                                                                                     .ToList();

                            if (oneOfsWithReference.Count == 1)
                            {
                                // Set the Reference directly instead and clear the OneOf collection
                                properties[context.PropertyKey].Reference = oneOfsWithReference.Single();
                                properties[context.PropertyKey].OneOf.Clear();
                            }
                        }
            },
            new("NotEmpty")
            {
                Matches = propertyValidator => propertyValidator is INotEmptyValidator,
                Apply = context =>
                        {
                            if (context.HasCondition)
                                return; //don't apply if this is a conditional rule

                            var schema = context.Schema;
                            var properties = schema.ActualProperties;
                            properties[context.PropertyKey].IsNullableRaw = false;
                            if (properties[context.PropertyKey].Type.HasFlag(JsonObjectType.Null))
                                properties[context.PropertyKey].Type &= ~JsonObjectType.Null; // Remove nullable
                            var oneOfsWithReference = properties[context.PropertyKey].OneOf
                                                                                     .Where(x => x.Reference != null)
                                                                                     .ToList();

                            if (oneOfsWithReference.Count == 1)
                            {
                                // Set the Reference directly instead and clear the OneOf collection
                                properties[context.PropertyKey].Reference = oneOfsWithReference.Single();
                                properties[context.PropertyKey].OneOf.Clear();
                            }
                            properties[context.PropertyKey].MinLength = 1;
                        }
            },
            new("Length")
            {
                Matches = propertyValidator => propertyValidator is ILengthValidator,
                Apply = context =>
                        {
                            var schema = context.Schema;
                            var properties = schema.ActualProperties;
                            var lengthValidator = (ILengthValidator)context.PropertyValidator;
                            if (lengthValidator.Max > 0)
                                properties[context.PropertyKey].MaxLength = lengthValidator.Max;
                            if (lengthValidator.GetType() == typeof(MinimumLengthValidator<>) ||
                                lengthValidator.GetType() == typeof(ExactLengthValidator<>) ||
                                properties[context.PropertyKey].MinLength is null or 1)
                                properties[context.PropertyKey].MinLength = lengthValidator.Min;
                        }
            },
            new("Pattern")
            {
                Matches = propertyValidator => propertyValidator is IRegularExpressionValidator,
                Apply = context =>
                        {
                            var regularExpressionValidator = (IRegularExpressionValidator)context.PropertyValidator;
                            var schema = context.Schema;
                            var properties = schema.ActualProperties;
                            properties[context.PropertyKey].Pattern = regularExpressionValidator.Expression;
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
                                var schema = context.Schema;
                                var properties = schema.ActualProperties;
                                var schemaProperty = properties[context.PropertyKey];

                                if (comparisonValidator.Comparison == Comparison.GreaterThanOrEqual)
                                    schemaProperty.Minimum = valueToCompare;
                                else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                                {
                                    schemaProperty.Minimum = valueToCompare;
                                    schemaProperty.IsExclusiveMinimum = true;
                                }
                                else if (comparisonValidator.Comparison == Comparison.LessThanOrEqual)
                                    schemaProperty.Maximum = valueToCompare;
                                else if (comparisonValidator.Comparison == Comparison.LessThan)
                                {
                                    schemaProperty.Maximum = valueToCompare;
                                    schemaProperty.IsExclusiveMaximum = true;
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
                            var schema = context.Schema;
                            var properties = schema.ActualProperties;
                            var schemaProperty = properties[context.PropertyKey];

                            if (betweenValidator.From.IsNumeric())
                            {
                                if (betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>)))
                                    schemaProperty.ExclusiveMinimum = Convert.ToDecimal(betweenValidator.From);
                                else
                                    schemaProperty.Minimum = Convert.ToDecimal(betweenValidator.From);
                            }

                            if (betweenValidator.To.IsNumeric())
                            {
                                if (betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>)))
                                    schemaProperty.ExclusiveMaximum = Convert.ToDecimal(betweenValidator.To);
                                else
                                    schemaProperty.Maximum = Convert.ToDecimal(betweenValidator.To);
                            }
                        }
            },
            new("AspNetCoreCompatibleEmail")
            {
                Matches = propertyValidator => propertyValidator.GetType().IsSubClassOfGeneric(typeof(AspNetCoreCompatibleEmailValidator<>)),
                Apply = context =>
                        {
                            var schema = context.Schema;
                            var properties = schema.ActualProperties;
                            properties[context.PropertyKey].Format = "email";
                            properties[context.PropertyKey].Pattern = "^[^@]+@[^@]+$"; // [^@] All chars except @
                        }
            }
        ];
}