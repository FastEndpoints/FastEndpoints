using System.Reflection;
using FastEndpoints.OpenApi.ValidationProcessor;
using FluentValidation.Validators;
using Microsoft.OpenApi;

namespace OpenApi;

public class ValidationRuleMappingTests
{
    [Fact]
    public void not_empty_uses_min_items_for_array_schemas()
    {
        var rules = GetDefaultRules();
        var propertySchema = new OpenApiSchema { Type = JsonSchemaType.Array };
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["tags"] = propertySchema
            }
        };
        var notEmptyRule = rules.Where(r => r.Matches(new StubNotEmptyValidator()))
                               .Single(
            r =>
            {
                var candidateSchema = new OpenApiSchema { Type = JsonSchemaType.Array };
                var candidateParent = new OpenApiSchema
                {
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["tags"] = candidateSchema
                    }
                };

                r.Apply(new(candidateParent, "tags", new StubNotEmptyValidator(), false));

                return candidateSchema.MinItems == 1;
            });

        notEmptyRule.Apply(new(schema, "tags", new StubNotEmptyValidator(), false));

        propertySchema.MinItems.ShouldBe(1);
        propertySchema.MinLength.ShouldBeNull();
    }

    [Fact]
    public void not_empty_does_not_emit_string_or_array_constraints_for_scalar_schemas()
    {
        var rules = GetDefaultRules();
        var propertySchema = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null };
        var schema = CreateParentSchema("age", propertySchema);
        var notEmptyRule = rules.Single(r => r.Matches(new StubNotEmptyValidator()) && MutatesNotEmptyConstraint(r, JsonSchemaType.String));

        notEmptyRule.Apply(new(schema, "age", new StubNotEmptyValidator(), false));

        propertySchema.Type.ShouldBe(JsonSchemaType.Integer);
        propertySchema.MinLength.ShouldBeNull();
        propertySchema.MinItems.ShouldBeNull();
    }

    [Fact]
    public void not_empty_uses_min_length_only_for_string_schemas()
    {
        var rules = GetDefaultRules();
        var propertySchema = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        var schema = CreateParentSchema("name", propertySchema);
        var notEmptyRule = rules.Single(r => r.Matches(new StubNotEmptyValidator()) && MutatesNotEmptyConstraint(r, JsonSchemaType.String));

        notEmptyRule.Apply(new(schema, "name", new StubNotEmptyValidator(), false));

        propertySchema.Type.ShouldBe(JsonSchemaType.String);
        propertySchema.MinLength.ShouldBe(1);
        propertySchema.MinItems.ShouldBeNull();
    }

    [Fact]
    public void length_rules_apply_string_constraints_only_to_string_schemas()
    {
        var rules = GetDefaultRules();
        var propertySchema = new OpenApiSchema { Type = JsonSchemaType.Integer };
        var schema = CreateParentSchema("age", propertySchema);
        var lengthValidator = CreateValidator<ILengthValidator>(typeof(ExactLengthValidator<>), 5);
        var lengthRule = rules.Single(r => r.Matches(lengthValidator));

        lengthRule.Apply(new(schema, "age", lengthValidator, false));

        propertySchema.MinLength.ShouldBeNull();
        propertySchema.MaxLength.ShouldBeNull();
        propertySchema.MinItems.ShouldBeNull();
        propertySchema.MaxItems.ShouldBeNull();
    }

    [Fact]
    public void closed_generic_minimum_length_validator_overrides_not_empty_min_length()
    {
        var rules = GetDefaultRules();
        var propertySchema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1 };
        var schema = CreateParentSchema("name", propertySchema);
        var lengthValidator = CreateValidator<ILengthValidator>(typeof(MinimumLengthValidator<>), 5);
        var lengthRule = rules.Single(r => r.Matches(lengthValidator));

        lengthRule.Apply(new(schema, "name", lengthValidator, false));

        propertySchema.MinLength.ShouldBe(5);
        propertySchema.MaxLength.ShouldBeNull();
    }

    [Fact]
    public void pattern_rule_does_not_apply_to_non_string_schema()
    {
        var rules = GetDefaultRules();
        var propertySchema = new OpenApiSchema { Type = JsonSchemaType.Boolean };
        var schema = CreateParentSchema("enabled", propertySchema);
        var regexValidator = new StubRegularExpressionValidator("^[a-z]+$");
        var patternRule = rules.Single(r => r.Matches(regexValidator));

        patternRule.Apply(new(schema, "enabled", regexValidator, false));

        propertySchema.Pattern.ShouldBeNull();
    }

    static FluentValidationRule[] GetDefaultRules()
        => (FluentValidationRule[])typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                .GetType("FastEndpoints.OpenApi.ValidationRuleCatalog", throwOnError: true)!
                                 .GetField("DefaultRules", BindingFlags.NonPublic | BindingFlags.Static)!
                                 .GetValue(null)!;

    static OpenApiSchema CreateParentSchema(string propertyName, OpenApiSchema propertySchema)
        => new()
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                [propertyName] = propertySchema
            }
        };

    static bool MutatesNotEmptyConstraint(FluentValidationRule rule, JsonSchemaType type)
    {
        var propertySchema = new OpenApiSchema { Type = type };
        var parentSchema = CreateParentSchema("value", propertySchema);

        rule.Apply(new(parentSchema, "value", new StubNotEmptyValidator(), false));

        return propertySchema.MinLength == 1 || propertySchema.MinItems == 1;
    }

    static TValidator CreateValidator<TValidator>(Type openGenericValidatorType, params object[] args)
        => (TValidator)Activator.CreateInstance(openGenericValidatorType.MakeGenericType(typeof(object)), args)!;

    sealed class StubNotEmptyValidator : INotEmptyValidator
    {
        public string Name => "NotEmptyValidator";

        public string GetDefaultMessageTemplate(string errorCode)
            => string.Empty;
    }

    sealed class StubRegularExpressionValidator(string expression) : IRegularExpressionValidator
    {
        public string Expression { get; } = expression;

        public string Name => "RegularExpressionValidator";

        public string GetDefaultMessageTemplate(string errorCode)
            => string.Empty;
    }
}
