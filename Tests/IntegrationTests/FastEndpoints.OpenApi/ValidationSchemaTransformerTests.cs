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
    public void outer_when_does_not_make_nullable_collection_unconditionally_required_or_non_empty()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        (schema.Required?.Contains("conditionalItems") ?? false).ShouldBeFalse();
        var propertySchema = GetPropertySchema(schema, "conditionalItems");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        propertySchema.MinItems.ShouldBeNull();
    }

    [Fact]
    public void outer_when_does_not_make_nullable_property_unconditionally_required_or_non_null()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        (schema.Required?.Contains("conditionalName") ?? false).ShouldBeFalse();
        GetPropertySchema(schema, "conditionalName").Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
    }

    [Fact]
    public void outer_when_async_does_not_make_nullable_property_unconditionally_required_or_non_null()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        (schema.Required?.Contains("asyncName") ?? false).ShouldBeFalse();
        GetPropertySchema(schema, "asyncName").Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
    }

    [Fact]
    public void outer_unless_does_not_make_nullable_property_unconditionally_required_or_non_null()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        (schema.Required?.Contains("unlessName") ?? false).ShouldBeFalse();
        GetPropertySchema(schema, "unlessName").Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
    }

    [Fact]
    public void current_validator_condition_only_conditions_that_component()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        (schema.Required?.Contains("componentName") ?? false).ShouldBeFalse();
        var propertySchema = GetPropertySchema(schema, "componentName");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        propertySchema.MinLength.ShouldBe(5);
    }

    [Fact]
    public void unconditional_not_empty_keeps_existing_required_non_null_and_minimum_behavior()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        schema.Required!.ShouldContain("requiredItems");
        var propertySchema = GetPropertySchema(schema, "requiredItems");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeFalse();
        propertySchema.MinItems.ShouldBe(1);
    }

    [Fact]
    public void independent_unconditional_presence_rule_still_makes_property_required()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        schema.Required!.ShouldContain("mixedItems");
        var propertySchema = GetPropertySchema(schema, "mixedItems");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeFalse();
        propertySchema.MinItems.ShouldBeNull();
    }

    [Fact]
    public void conditional_not_empty_with_child_rules_does_not_make_parent_collection_required()
    {
        var schema = CreateConditionalRulesSchema();

        ApplyValidatorToSchema(schema, new ConditionalRulesValidator());

        (schema.Required?.Contains("connectedTools") ?? false).ShouldBeFalse();
        var propertySchema = GetPropertySchema(schema, "connectedTools");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        propertySchema.MinItems.ShouldBeNull();
    }

    [Fact]
    public void validator_does_not_inline_referenced_leaf_schema_without_applicable_rules()
    {
        var document = new OpenApiDocument
        {
            Components = new()
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["EnumRuleValue"] = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            }
        };
        var schema = new OpenApiSchema
        {
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["test"] = new OpenApiSchemaReference("EnumRuleValue", document)
            }
        };

        ApplyValidatorToSchema(schema, new EnumRuleValidator(), localizeReferencedSchemas: true);

        schema.Properties["test"].ShouldBeOfType<OpenApiSchemaReference>();
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

    static OpenApiSchema CreateConditionalRulesSchema()
        => new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["conditionalItems"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["conditionalName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["asyncName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["unlessName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["componentName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
                ["requiredItems"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["mixedItems"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["connectedTools"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["toolName"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null }
                        }
                    }
                }
            }
        };

    static OpenApiSchema GetPropertySchema(OpenApiSchema schema, string propertyName)
        => (OpenApiSchema)schema.Properties![propertyName];

    static void ApplyValidatorToSchema(OpenApiSchema schema, IValidator validator, bool localizeReferencedSchemas = false)
    {
        var applierType = typeof(FastEndpoints.OpenApi.Extensions)
                          .Assembly
                          .GetType("FastEndpoints.OpenApi.ValidationSchemaApplier", throwOnError: true)!;

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
                "GET:/test",
                "requestBody",
                localizeReferencedSchemas
            ],
            culture: null)!;

        applierType.GetMethod("ApplyValidator", BindingFlags.Instance | BindingFlags.NonPublic)!
                   .Invoke(applier, [schema, validator, string.Empty, new HashSet<Type>()]);
    }

    sealed class ConditionalRulesRequest
    {
        [JsonPropertyName("conditionalItems")]
        public List<string>? ConditionalItems { get; init; }

        [JsonPropertyName("conditionalName")]
        public string? ConditionalName { get; init; }

        [JsonPropertyName("asyncName")]
        public string? AsyncName { get; init; }

        [JsonPropertyName("unlessName")]
        public string? UnlessName { get; init; }

        public bool SkipUnlessRule { get; init; }

        [JsonPropertyName("componentName")]
        public string? ComponentName { get; init; }

        [JsonPropertyName("requiredItems")]
        public List<string>? RequiredItems { get; init; }

        [JsonPropertyName("mixedItems")]
        public List<string>? MixedItems { get; init; }

        [JsonPropertyName("connectedTools")]
        public List<ConnectedTool>? ConnectedTools { get; init; }
    }

    sealed class ConnectedTool
    {
        [JsonPropertyName("toolName")]
        public string? ToolName { get; init; }
    }

    sealed class ConditionalRulesValidator : Validator<ConditionalRulesRequest>
    {
        public ConditionalRulesValidator()
        {
            When(x => x.ConditionalItems is not null, () =>
            {
                RuleFor(x => x.ConditionalItems).NotEmpty();
            });

            When(x => x.ConditionalName is not null, () =>
            {
                RuleFor(x => x.ConditionalName).NotNull();
            });

            WhenAsync((x, _) => Task.FromResult(x.AsyncName is not null), () =>
            {
                RuleFor(x => x.AsyncName).NotNull();
            });

            Unless(x => x.SkipUnlessRule, () =>
            {
                RuleFor(x => x.UnlessName).NotNull();
            });

            RuleFor(x => x.ComponentName)
                .MinimumLength(5)
                .NotEmpty()
                .When(x => x.ComponentName is not null, ApplyConditionTo.CurrentValidator);

            RuleFor(x => x.RequiredItems).NotEmpty();

            When(x => x.MixedItems is not null, () =>
            {
                RuleFor(x => x.MixedItems).NotEmpty();
            });
            RuleFor(x => x.MixedItems).NotNull();

            When(x => x.ConnectedTools is not null, () =>
            {
                RuleFor(x => x.ConnectedTools).NotEmpty();
                RuleForEach(x => x.ConnectedTools)
                    .ChildRules(tool =>
                    {
                        tool.RuleFor(x => x.ToolName).NotEmpty();
                    });
            });
        }
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

    sealed class EnumRuleRequest
    {
        public EnumRuleValue Test { get; init; }
    }

    sealed class EnumRuleValidator : Validator<EnumRuleRequest>
    {
        public EnumRuleValidator()
        {
            RuleFor(x => x.Test).IsInEnum();
        }
    }

    enum EnumRuleValue
    {
        First,
        Second
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
