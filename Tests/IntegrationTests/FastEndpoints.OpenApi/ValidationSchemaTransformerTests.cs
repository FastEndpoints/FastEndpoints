using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.OpenApi;

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
            args: [new DocumentOptions(), new SharedContext()],
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

    [Fact]
    public void validator_rule_paths_do_not_apply_naming_policy_when_disabled()
    {
        var rules = new NamingPolicyCacheValidator().GetDictionaryOfRules(new PrefixNamingPolicy("x_"), false, typeof(NamingPolicyCacheRequest));

        rules.ContainsKey("Name").ShouldBeTrue();
        rules.ContainsKey("x_Name").ShouldBeFalse();
    }

    [Fact]
    public void validator_rule_paths_keep_json_property_name_attributes_when_naming_policy_is_disabled()
    {
        var rules = new JsonPropertyNameRulePathValidator().GetDictionaryOfRules(
            JsonNamingPolicy.CamelCase,
            false,
            typeof(JsonPropertyNameRulePathRequest));

        rules.ContainsKey("point_data.x_coord").ShouldBeTrue();
    }

    [Fact]
    public void validator_rule_paths_normalize_collection_indexers_and_item_property_names()
    {
        var rules = new CollectionRulePathValidator().GetDictionaryOfRules(typeof(CollectionRulePathRequest));

        rules.ContainsKey("line_items[].product_name").ShouldBeTrue();
        rules.ContainsKey("indexed_items[].product_name").ShouldBeTrue();
    }

    [Fact]
    public void validation_rules_apply_to_collection_item_schema_properties()
    {
        var itemNameSchema = new OpenApiSchema { Type = JsonSchemaType.String };
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["indexed_items"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["product_name"] = itemNameSchema
                        }
                    }
                }
            }
        };

        ApplyValidatorToSchema(schema, new IndexedCollectionRulePathValidator());

        itemNameSchema.MinLength.ShouldBe(1);
    }

    [Fact]
    public void cyclic_included_validators_do_not_recurse_forever_when_rules_are_cached()
    {
        var transformerType = typeof(FastEndpoints.OpenApi.Extensions).Assembly
                                                                      .GetType("FastEndpoints.OpenApi.ValidationSchemaTransformer", throwOnError: true)!;
        var transformer = Activator.CreateInstance(
            transformerType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args: [new DocumentOptions(), new SharedContext()],
            culture: null)!;
        var cacheRules = transformerType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                                        .Single(m => m.Name == "CacheValidatorRules" && m.GetParameters().Length == 2);

        var cachedRules = cacheRules.Invoke(transformer, [new SelfIncludeValidator(), null])!;

        HasRule(cachedRules, "Name").ShouldBeTrue();
        GetIncludedRuleCount(cachedRules).ShouldBe(0);
    }

    static bool HasRule(object cachedRules, string key)
    {
        var rules = cachedRules.GetType().GetProperty("Rules")!.GetValue(cachedRules)!;

        return (bool)rules.GetType().GetMethod("ContainsKey")!.Invoke(rules, [key])!;
    }

    static int GetIncludedRuleCount(object cachedRules)
    {
        var includedRules = (Array)cachedRules.GetType().GetProperty("IncludedRules")!.GetValue(cachedRules)!;

        return includedRules.Length;
    }

    static void ApplyValidatorToSchema(OpenApiSchema schema, IValidator validator)
    {
        var applierType = typeof(FastEndpoints.OpenApi.Extensions)
                          .Assembly
                          .GetType("FastEndpoints.OpenApi.ValidationSchemaTransformer+ValidationSchemaApplier", throwOnError: true)!;

        var rules = typeof(FastEndpoints.OpenApi.Extensions)
                    .Assembly
                    .GetType("FastEndpoints.OpenApi.ValidationRuleCatalog", throwOnError: true)!
                    .GetField("DefaultRules", BindingFlags.NonPublic | BindingFlags.Static)!
                    .GetValue(null)!;
        var resolver = new TestServiceResolver();
        using var applier = (IDisposable)Activator.CreateInstance(
            applierType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args:
            [
                new SharedContext(),
                resolver,
                null,
                (Func<IServiceScope>)(() => resolver.CreateScope()),
                rules,
                true,
                false
            ],
            culture: null)!;

        applierType.GetMethod("ApplyValidator", BindingFlags.Instance | BindingFlags.NonPublic)!
                   .Invoke(applier, [schema, validator, string.Empty, new HashSet<Type>()]);
    }

    sealed class JsonPropertyNameRulePathRequest
    {
        [JsonPropertyName("point_data")]
        public JsonPropertyNameRulePathPoint PointData { get; } = new();
    }

    sealed class JsonPropertyNameRulePathPoint
    {
        [JsonPropertyName("x_coord")]
        public int XCoord { get; set; }
    }

    sealed class JsonPropertyNameRulePathValidator : Validator<JsonPropertyNameRulePathRequest>
    {
        public JsonPropertyNameRulePathValidator()
        {
            RuleFor(x => x.PointData.XCoord).GreaterThan(0);
        }
    }

    sealed class CollectionRulePathRequest
    {
        [JsonPropertyName("line_items")]
        public List<CollectionRulePathItem> LineItems { get; } = [];

        [JsonPropertyName("indexed_items")]
        public CollectionRulePathItem[] IndexedItems { get; } = [];
    }

    sealed class CollectionRulePathItem
    {
        [JsonPropertyName("product_name")]
        public string ProductName { get; set; } = string.Empty;
    }

    sealed class CollectionRulePathValidator : Validator<CollectionRulePathRequest>
    {
        public CollectionRulePathValidator()
        {
            RuleFor(x => x.LineItems).NotEmpty().OverridePropertyName("LineItems[].ProductName");
            RuleFor(x => x.IndexedItems).NotEmpty().OverridePropertyName("IndexedItems[0].ProductName");
        }
    }

    sealed class IndexedCollectionRulePathValidator : Validator<CollectionRulePathRequest>
    {
        public IndexedCollectionRulePathValidator()
        {
            RuleFor(x => x.IndexedItems).NotEmpty().OverridePropertyName("IndexedItems[0].ProductName");
        }
    }

    sealed class NamingPolicyCacheRequest
    {
        public string Name { get; } = string.Empty;
    }

    sealed class NamingPolicyCacheValidator : Validator<NamingPolicyCacheRequest>
    {
        public NamingPolicyCacheValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }

    sealed class SelfIncludeValidator : Validator<NamingPolicyCacheRequest>
    {
        public SelfIncludeValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            Include(this);
        }
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