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

using FastEndpoints.Swagger.ValidationProcessor;
using FastEndpoints.Swagger.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NJsonSchema;
using NJsonSchema.Generation;

namespace FastEndpoints.Swagger;

public class ValidationSchemaProcessor : ISchemaProcessor
{
    private readonly FluentValidationRule[] _rules;
    private readonly IServiceProvider? _serviceProvider = IServiceResolver.ServiceProvider?.CreateScope().ServiceProvider;

    public ValidationSchemaProcessor()
    {
        _rules = CreateDefaultRules();
    }

    public void Process(SchemaProcessorContext context)
    {
        var tRequest = context.Type;

        foreach (var e in MainExtensions.Endpoints.Found)
        {
            if (e.ValidatorType?.BaseType?.GenericTypeArguments.FirstOrDefault() == tRequest)
            {
                var validator = _serviceProvider?.GetRequiredService(e.ValidatorType);
                if (validator is null)
                    throw new InvalidOperationException($"Please call app.{nameof(MainExtensions.UseFastEndpoints)}() before calling app.{nameof(NSwagApplicationBuilderExtensions.UseOpenApi)}()");
                ApplyRulesToSchema(context.Schema, (IValidator)validator);
                break;
            }
        }
    }

    private void ApplyRulesToSchema(JsonSchema? schema, IValidator validator)
    {
        if (schema is null)
            return;

        // Add properties from current schema/class
        if (schema.ActualProperties != null)
        {
            foreach (var schemaProperty in schema.ActualProperties.Keys)
                TryApplyValidation(schema, validator, schemaProperty);
        }

        // Add properties from base class
        ApplyRulesToSchema(schema.InheritedSchema, validator);
    }

    private void TryApplyValidation(JsonSchema schema, IValidator validator, string schemaProperty)
    {
        foreach (var propertyValidator in validator.GetValidatorsForMemberIgnoreCase(schemaProperty))
        {
            foreach (var rule in _rules)
            {
                if (!rule.Matches(propertyValidator))
                    continue;

                try
                {
                    rule.Apply(new RuleContext(schema, schemaProperty, propertyValidator));
                }
                finally
                {
                    // do nothing
                }
            }
        }
    }

    private static FluentValidationRule[] CreateDefaultRules() => new[]
    {
        new FluentValidationRule("Required")
        {
            Matches = propertyValidator => propertyValidator is INotNullValidator or INotEmptyValidator,
            Apply = context =>
            {
                var schema = context.Schema;
                if (schema == null)
                    return;
                if (!schema.RequiredProperties.Contains(context.PropertyKey))
                    schema.RequiredProperties.Add(context.PropertyKey);
            }
        },
        new FluentValidationRule("NotNull")
        {
            Matches = propertyValidator => propertyValidator is INotNullValidator,
            Apply = context =>
            {
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
        new FluentValidationRule("NotEmpty")
        {
            Matches = propertyValidator => propertyValidator is INotEmptyValidator,
            Apply = context =>
            {
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
        new FluentValidationRule("Length")
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
                    properties[context.PropertyKey].MinLength == null)
                {
                    properties[context.PropertyKey].MinLength = lengthValidator.Min;
                }
            }
        },
        new FluentValidationRule("Pattern")
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
        new FluentValidationRule("Comparison")
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
                    {
                        schemaProperty.Minimum = valueToCompare;
                    }
                    else if (comparisonValidator.Comparison == Comparison.GreaterThan)
                    {
                        schemaProperty.Minimum = valueToCompare;
                        schemaProperty.IsExclusiveMinimum = true;
                    }
                    else if (comparisonValidator.Comparison == Comparison.LessThanOrEqual) { schemaProperty.Maximum = valueToCompare; } else if (comparisonValidator.Comparison == Comparison.LessThan)
                    {
                        schemaProperty.Maximum = valueToCompare;
                        schemaProperty.IsExclusiveMaximum = true;
                    }
                }
            }
        },
        new FluentValidationRule("Between")
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
        new FluentValidationRule("AspNetCoreCompatibleEmail")
        {
            Matches = propertyValidator => propertyValidator.GetType().IsSubClassOfGeneric(typeof(AspNetCoreCompatibleEmailValidator<>)),
            Apply = context =>
            {
                var schema = context.Schema;
                var properties = schema.ActualProperties;
                properties[context.PropertyKey].Pattern = "^[^@]+@[^@]+$"; // [^@] All chars except @
            }
        },
    };
}