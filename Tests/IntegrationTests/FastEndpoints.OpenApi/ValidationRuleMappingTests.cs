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

    static FluentValidationRule[] GetDefaultRules()
        => (FluentValidationRule[])typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                .GetType("FastEndpoints.OpenApi.ValidationSchemaTransformer", throwOnError: true)!
                                .GetMethod("CreateDefaultRules", BindingFlags.NonPublic | BindingFlags.Static)!
                                .Invoke(null, null)!;

    sealed class StubNotEmptyValidator : INotEmptyValidator
    {
        public string Name => "NotEmptyValidator";

        public string GetDefaultMessageTemplate(string errorCode)
            => string.Empty;
    }
}
