using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
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
                        if (!context.HasCondition)
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

                        if (context.TryGetPropertySchema(out var prop) &&
                            prop.Type is not null &&
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

                        if (IsArraySchema(prop))
                            prop.MinItems = 1;
                        else if (IsStringSchema(prop))
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

                        if (IsArraySchema(target))
                        {
                            if (lengthValidator.Max > 0)
                                target.MaxItems = lengthValidator.Max;
                            if (IsValidatorType(lengthValidator, typeof(MinimumLengthValidator<>)) ||
                                IsValidatorType(lengthValidator, typeof(ExactLengthValidator<>)) ||
                                target.MinItems is null or 1)
                                target.MinItems = lengthValidator.Min;

                            return;
                        }

                        if (!IsStringSchema(target))
                            return;

                        if (lengthValidator.Max > 0)
                            target.MaxLength = lengthValidator.Max;
                        if (IsValidatorType(lengthValidator, typeof(MinimumLengthValidator<>)) ||
                            IsValidatorType(lengthValidator, typeof(ExactLengthValidator<>)) ||
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

                        if (!IsStringSchema(prop))
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
                            var valueStr = comparisonValidator.ValueToCompare.ToInvariantNumericString();

                            if (!context.TryGetPropertySchema(out var prop))
                                return;

                            // ReSharper disable once SwitchStatementMissingSomeEnumCasesNoDefault
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

                        var isExclusive = betweenValidator.GetType().IsSubClassOfGeneric(typeof(ExclusiveBetweenValidator<,>));

                        if (betweenValidator.From.IsNumeric())
                        {
                            var fromStr = betweenValidator.From.ToInvariantNumericString();
                            if (isExclusive)
                                prop.ExclusiveMinimum = fromStr;
                            else
                                prop.Minimum = fromStr;
                        }

                        if (betweenValidator.To.IsNumeric())
                        {
                            var toStr = betweenValidator.To.ToInvariantNumericString();
                            if (isExclusive)
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

                        if (IsStringSchema(prop))
                        {
                            prop.Format = "email";
                            prop.Pattern = "^[^@]+@[^@]+$";
                        }
                    }
        }
    ];

    static bool IsStringSchema(OpenApiSchema schema)
        => schema.Type?.HasFlag(JsonSchemaType.String) == true;

    static bool IsArraySchema(OpenApiSchema schema)
        => schema.Type?.HasFlag(JsonSchemaType.Array) == true;

    static bool IsValidatorType(object validator, Type openGenericValidatorType)
    {
        var validatorType = validator.GetType();

        return (validatorType.IsGenericType && validatorType.GetGenericTypeDefinition() == openGenericValidatorType) ||
               validatorType.IsSubClassOfGeneric(openGenericValidatorType);
    }
}
