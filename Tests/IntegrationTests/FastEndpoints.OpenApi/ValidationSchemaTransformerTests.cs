using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void validator_rule_cache_keeps_naming_policy_context_separate()
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                               .GetType("FastEndpoints.OpenApi.ValidationSchemaTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new FastEndpoints.OpenApi.SharedContext()],
            culture: null)!;
        transformerType.GetField("_serviceResolver", BindingFlags.Instance | BindingFlags.NonPublic)!
                       .SetValue(transformer, new TestServiceResolver());
        var getRules = transformerType.GetMethod("GetOrCreateValidatorRules", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var xRules = getRules.Invoke(transformer, [typeof(NamingPolicyCacheValidator), new PrefixNamingPolicy("x_")])!;
        var yRules = getRules.Invoke(transformer, [typeof(NamingPolicyCacheValidator), new PrefixNamingPolicy("y_")])!;

        HasRule(xRules, "x_Name").ShouldBeTrue();
        HasRule(xRules, "y_Name").ShouldBeFalse();
        HasRule(yRules, "y_Name").ShouldBeTrue();
        HasRule(yRules, "x_Name").ShouldBeFalse();
    }

    static bool HasRule(object cachedRules, string key)
    {
        var rules = cachedRules.GetType().GetProperty("Rules")!.GetValue(cachedRules)!;

        return (bool)rules.GetType().GetMethod("ContainsKey")!.Invoke(rules, [key])!;
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

    sealed class NamingPolicyCacheRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    sealed class NamingPolicyCacheValidator : Validator<NamingPolicyCacheRequest>
    {
        public NamingPolicyCacheValidator()
            => RuleFor(x => x.Name).NotEmpty();
    }

    sealed class PrefixNamingPolicy(string prefix) : JsonNamingPolicy
    {
        public override string ConvertName(string name)
            => prefix + name;
    }

    sealed class TestServiceResolver : IServiceResolver
    {
        public IServiceScope CreateScope()
            => new TestScope();

        public TService? TryResolve<TService>() where TService : class
            => null;

        public object? TryResolve(Type typeOfService)
            => null;

        public TService Resolve<TService>() where TService : class
            => throw new InvalidOperationException();

        public object Resolve(Type typeOfService)
            => throw new InvalidOperationException();

        public TService? TryResolve<TService>(string keyName) where TService : class
            => null;

        public object? TryResolve(Type typeOfService, string keyName)
            => null;

        public TService Resolve<TService>(string keyName) where TService : class
            => throw new InvalidOperationException();

        public object Resolve(Type typeOfService, string keyName)
            => throw new InvalidOperationException();

        public object CreateInstance(Type type, IServiceProvider? serviceProvider = null)
            => Activator.CreateInstance(type)!;

        public object CreateSingleton(Type type)
            => CreateInstance(type);
    }

    sealed class TestScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();

        public void Dispose()
        {
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();
        }
    }

}
