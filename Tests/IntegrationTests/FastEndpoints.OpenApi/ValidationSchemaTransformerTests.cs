using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Web;

namespace OpenApi;

public class ValidationSchemaTransformerTests
{
    [Fact]
    public void validator_rule_paths_use_json_property_name_attributes()
    {
        var rules = new JsonPropertyNameRulePathValidator().GetDictionaryOfRules(typeof(JsonPropertyNameRulePathRequest));

        rules.ContainsKey("point_data.x_coord").ShouldBeTrue();
    }

    [Fact]
    public void swagger_review_validator_uses_json_property_name_attributes()
    {
        var validatorType = typeof(Web.Program).Assembly.GetType("TestCases.Swagger.Review.JsonPropertyNameTransformerReviewValidator", throwOnError: true)!;
        var requestType = typeof(Web.Program).Assembly.GetType("TestCases.Swagger.Review.JsonPropertyNameTransformerReviewRequest", throwOnError: true)!;
        var validator = (IValidator)Activator.CreateInstance(validatorType, nonPublic: true)!;
        var rules = validator.GetDictionaryOfRules(requestType);

        rules.ContainsKey("x_coord").ShouldBeTrue();
    }

    sealed class JsonPropertyNameRulePathRequest
    {
        [JsonPropertyName("point_data")]
        public JsonPropertyNameRulePathPoint PointData { get; set; } = new();
    }

    sealed class JsonPropertyNameRulePathPoint
    {
        [JsonPropertyName("x_coord")]
        public int XCoord { get; set; }
    }

    sealed class JsonPropertyNameRulePathValidator : Validator<JsonPropertyNameRulePathRequest>
    {
        public JsonPropertyNameRulePathValidator()
            => RuleFor(x => x.PointData.XCoord).GreaterThan(0);
    }
}
