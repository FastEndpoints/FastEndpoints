using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class ValidationRuleCatalog
{
    internal static readonly FluentValidationRule[] DefaultRules =
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
