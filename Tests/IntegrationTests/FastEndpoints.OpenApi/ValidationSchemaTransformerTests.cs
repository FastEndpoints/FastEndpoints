using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.OpenApi;

namespace OpenApi;

public class ValidationSchemaTransformerTests : DefaultPropertyNameResolverTestBase
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
    public void rule_for_each_not_empty_applies_to_items_instead_of_the_collection()
    {
        var schema = CreateRuleForEachSchema();

        ApplyValidatorToSchema(schema, new RuleForEachValidator());

        (schema.Required?.Contains("tags") ?? false).ShouldBeFalse();
        var propertySchema = GetPropertySchema(schema, "tags");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        propertySchema.MinItems.ShouldBeNull();
        var itemsSchema = (OpenApiSchema)propertySchema.Items!;
        itemsSchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeFalse();
        itemsSchema.MinLength.ShouldBe(1);
    }

    [Fact]
    public void rule_for_each_length_and_range_apply_to_items_instead_of_the_collection()
    {
        var schema = CreateRuleForEachSchema();

        ApplyValidatorToSchema(schema, new RuleForEachValidator());

        var codesSchema = GetPropertySchema(schema, "codes");
        codesSchema.MaxItems.ShouldBeNull();
        codesSchema.MinItems.ShouldBeNull();
        ((OpenApiSchema)codesSchema.Items!).MaxLength.ShouldBe(20);

        var scoresSchema = GetPropertySchema(schema, "scores");
        scoresSchema.Minimum.ShouldBeNull();
        scoresSchema.Maximum.ShouldBeNull();
        var scoreItemsSchema = (OpenApiSchema)scoresSchema.Items!;
        scoreItemsSchema.Minimum.ShouldBe("1");
        scoreItemsSchema.Maximum.ShouldBe("5");
    }

    [Fact]
    public void rule_for_and_rule_for_each_on_the_same_collection_keep_their_own_targets()
    {
        var schema = CreateRuleForEachSchema();

        ApplyValidatorToSchema(schema, new RuleForEachValidator());

        schema.Required!.ShouldContain("labels");
        var propertySchema = GetPropertySchema(schema, "labels");
        propertySchema.MinItems.ShouldBe(1);
        propertySchema.MaxItems.ShouldBeNull();
        ((OpenApiSchema)propertySchema.Items!).MaxLength.ShouldBe(5);
    }

    [Fact]
    public void rule_for_each_localizes_referenced_items_per_property_and_operation()
    {
        var sharedItems = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        var document = new OpenApiDocument
        {
            Components = new()
            {
                Schemas = new Dictionary<string, IOpenApiSchema> { ["Tag"] = sharedItems }
            }
        };
        var sharedCtx = new SharedContext();
        var firstSchema = CreateRuleForEachSchema();
        GetPropertySchema(firstSchema, "tags").Items = new OpenApiSchemaReference("Tag", document);
        GetPropertySchema(firstSchema, "codes").Items = new OpenApiSchemaReference("Tag", document);

        var generation = ApplyValidatorToSchema(firstSchema, new RuleForEachValidator(), true, sharedCtx, document);

        var tagsRef = GetPropertySchema(firstSchema, "tags").Items.ShouldBeOfType<OpenApiSchemaReference>();
        var codesRef = GetPropertySchema(firstSchema, "codes").Items.ShouldBeOfType<OpenApiSchemaReference>();
        tagsRef.Reference.Id.ShouldNotBe(codesRef.Reference.Id);
        var tagsItems = generation.OperationSchemaVariants[tagsRef.Reference.Id!];
        var codesItems = generation.OperationSchemaVariants[codesRef.Reference.Id!];
        tagsItems.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeFalse();
        tagsItems.MinLength.ShouldBe(1);
        tagsItems.MaxLength.ShouldBeNull();
        codesItems.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        codesItems.MaxLength.ShouldBe(20);

        var secondSchema = CreateRuleForEachSchema();
        GetPropertySchema(secondSchema, "codes").Items = new OpenApiSchemaReference("Tag", document);
        var secondValidator = new InlineValidator<RuleForEachRequest>();
        secondValidator.RuleForEach(x => x.Codes).MaximumLength(5);

        ApplyValidatorToSchema(secondSchema, secondValidator, true, sharedCtx, document, "GET:/other");

        var secondCodesRef = GetPropertySchema(secondSchema, "codes").Items.ShouldBeOfType<OpenApiSchemaReference>();
        secondCodesRef.Reference.Id.ShouldNotBe(codesRef.Reference.Id);
        var secondCodesItems = generation.OperationSchemaVariants[secondCodesRef.Reference.Id!];
        secondCodesItems.ShouldNotBeSameAs(codesItems);
        secondCodesItems.MaxLength.ShouldBe(5);
        codesItems.MaxLength.ShouldBe(20);
        sharedItems.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        sharedItems.MinLength.ShouldBeNull();
        sharedItems.MaxLength.ShouldBeNull();
        document.Components.Schemas["Tag"].ShouldBeSameAs(sharedItems);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void scalar_item_and_child_validator_rules_reuse_the_same_referenced_item_variant(bool childRuleFirst)
    {
        var sharedName = new OpenApiSchema { Type = JsonSchemaType.String };
        var sharedItems = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            Properties = new Dictionary<string, IOpenApiSchema> { ["name"] = sharedName }
        };
        var sharedArray = new OpenApiSchema { Type = JsonSchemaType.Array };
        var document = new OpenApiDocument
        {
            Components = new()
            {
                Schemas = new Dictionary<string, IOpenApiSchema>
                {
                    ["Tag"] = sharedItems,
                    ["Tags"] = sharedArray
                }
            }
        };
        sharedArray.Items = new OpenApiSchemaReference("Tag", document);
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["tags"] = new OpenApiSchemaReference("Tags", document)
            }
        };
        var validator = new InlineValidator<TagItemsRequest>();
        if (childRuleFirst)
            validator.RuleForEach(x => x.Tags).SetValidator(new TagItemValidator());
        validator.RuleForEach(x => x.Tags).NotNull();
        if (!childRuleFirst)
            validator.RuleForEach(x => x.Tags).SetValidator(new TagItemValidator());

        var generation = ApplyValidatorToSchema(schema, validator, true, document: document);

        var arrayRef = schema.Properties["tags"].ShouldBeOfType<OpenApiSchemaReference>();
        var arrayVariant = generation.OperationSchemaVariants[arrayRef.Reference.Id!];
        var itemsRef = arrayVariant.Items.ShouldBeOfType<OpenApiSchemaReference>();
        var itemVariant = generation.OperationSchemaVariants[itemsRef.Reference.Id!];
        itemVariant.Type.ShouldBe(JsonSchemaType.Object);
        GetPropertySchema(itemVariant, "name").MaxLength.ShouldBe(5);
        generation.OperationSchemaVariants.Count.ShouldBe(2);
        sharedItems.Type.ShouldBe(JsonSchemaType.Object | JsonSchemaType.Null);
        sharedName.MaxLength.ShouldBeNull();
        sharedArray.Items.ShouldBeOfType<OpenApiSchemaReference>().Reference.Id.ShouldBe("Tag");
        document.Components.Schemas["Tag"].ShouldBeSameAs(sharedItems);
        document.Components.Schemas["Tags"].ShouldBeSameAs(sharedArray);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void rule_for_each_nullable_wrappers_localize_array_and_item_references(bool nullFirst)
    {
        var sharedItems = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        var document = new OpenApiDocument
        {
            Components = new()
            {
                Schemas = new Dictionary<string, IOpenApiSchema> { ["Tag"] = sharedItems }
            }
        };
        var sharedArray = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchemaReference("Tag", document)
        };
        document.Components.Schemas["Tags"] = sharedArray;
        var schema = CreateRuleForEachSchema();

        foreach (var name in new[] { "tags", "codes" })
        {
            var nullOption = new OpenApiSchema { Type = JsonSchemaType.Null };
            var arrayOption = new OpenApiSchemaReference("Tags", document) { Description = name };
            schema.Properties![name] = new OpenApiSchema
            {
                OneOf = nullFirst ? [nullOption, arrayOption] : [arrayOption, nullOption]
            };
        }

        var generation = ApplyValidatorToSchema(schema, new RuleForEachValidator(), true, document: document);
        var tagsWrapper = GetPropertySchema(schema, "tags");
        var codesWrapper = GetPropertySchema(schema, "codes");
        var arrayIndex = nullFirst ? 1 : 0;
        var tagsArray = tagsWrapper.OneOf![arrayIndex].ShouldBeOfType<OpenApiSchema>();
        var codesArray = codesWrapper.OneOf![arrayIndex].ShouldBeOfType<OpenApiSchema>();
        tagsArray.ShouldNotBeSameAs(codesArray);
        tagsArray.ShouldNotBeSameAs(sharedArray);
        tagsArray.Description.ShouldBe("tags");
        codesArray.Description.ShouldBe("codes");
        tagsWrapper.OneOf.Count.ShouldBe(2);
        tagsWrapper.OneOf[1 - arrayIndex].Type.ShouldBe(JsonSchemaType.Null);
        tagsWrapper.Type.ShouldBeNull();
        (schema.Required?.Contains("tags") ?? false).ShouldBeFalse();

        var tagsRef = tagsArray.Items.ShouldBeOfType<OpenApiSchemaReference>();
        var codesRef = codesArray.Items.ShouldBeOfType<OpenApiSchemaReference>();
        tagsRef.Reference.Id.ShouldNotBe(codesRef.Reference.Id);
        var tagsItems = generation.OperationSchemaVariants[tagsRef.Reference.Id!];
        var codesItems = generation.OperationSchemaVariants[codesRef.Reference.Id!];
        tagsItems.MinLength.ShouldBe(1);
        tagsItems.Type.ShouldBe(JsonSchemaType.String);
        codesItems.MaxLength.ShouldBe(20);
        codesItems.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        sharedArray.Items.ShouldBeOfType<OpenApiSchemaReference>().Reference.Id.ShouldBe("Tag");
        sharedItems.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        sharedItems.MinLength.ShouldBeNull();
        sharedItems.MaxLength.ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void rule_for_each_leaves_multi_type_collection_unions_unchanged(bool includeNull)
    {
        var items = new OpenApiSchema { Type = JsonSchemaType.String };
        var array = new OpenApiSchema { Type = JsonSchemaType.Array, Items = items };
        var other = new OpenApiSchema { Type = JsonSchemaType.String };
        var wrapper = new OpenApiSchema
        {
            OneOf = includeNull ? [new OpenApiSchema { Type = JsonSchemaType.Null }, array, other] : [array, other]
        };
        var schema = CreateRuleForEachSchema();
        schema.Properties!["tags"] = wrapper;
        var validator = new InlineValidator<RuleForEachRequest>();
        validator.RuleForEach(x => x.Tags).MaximumLength(5);

        var generation = ApplyValidatorToSchema(schema, validator, true);

        wrapper.OneOf.ShouldContain(array);
        array.Items.ShouldBeSameAs(items);
        items.MaxLength.ShouldBeNull();
        generation.OperationSchemaVariants.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void conditional_rule_for_each_presence_preserves_nullable_items(bool useOuterCondition, bool useNotEmpty)
    {
        var schema = CreateRuleForEachSchema();
        var validator = new InlineValidator<RuleForEachRequest>();

        if (useOuterCondition)
        {
            validator.When(x => x.Codes.Count > 0, () =>
            {
                if (useNotEmpty)
                    validator.RuleForEach(x => x.Tags).NotEmpty();
                else
                    validator.RuleForEach(x => x.Tags).NotNull();
            });
        }
        else if (useNotEmpty)
        {
            validator.RuleForEach(x => x.Tags)
                     .NotEmpty()
                     .When(x => x.Codes.Count > 0, ApplyConditionTo.CurrentValidator);
        }
        else
        {
            validator.RuleForEach(x => x.Tags)
                     .NotNull()
                     .When(x => x.Codes.Count > 0, ApplyConditionTo.CurrentValidator);
        }

        ApplyValidatorToSchema(schema, validator);

        (schema.Required?.Contains("tags") ?? false).ShouldBeFalse();
        var propertySchema = GetPropertySchema(schema, "tags");
        propertySchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        propertySchema.MinItems.ShouldBeNull();
        var itemsSchema = propertySchema.Items.ShouldBeOfType<OpenApiSchema>();
        itemsSchema.Type!.Value.HasFlag(JsonSchemaType.Null).ShouldBeTrue();
        itemsSchema.MinLength.ShouldBeNull();
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

    [Theory]
    [InlineData("When")]
    [InlineData("Unless")]
    [InlineData("WhenAsync")]
    [InlineData("UnlessAsync")]
    [InlineData("OuterWhen")]
    [InlineData("OuterUnless")]
    [InlineData("OuterWhenAsync")]
    [InlineData("OuterUnlessAsync")]
    public async Task conditional_rule_for_each_omits_pattern_length_and_numeric_constraints(string condition)
    {
        var enforce = false;
        var validator = new InlineValidator<RuleForEachRequest>();
        Action addRules = () =>
        {
            var pattern = validator.RuleForEach(x => x.Tags).Matches("^a+$");
            var length = validator.RuleForEach(x => x.Codes).Length(2, 5);
            var range = validator.RuleForEach(x => x.Scores).InclusiveBetween(1, 5).GreaterThan(0);

            switch (condition)
            {
                case "When":
                    pattern.When(_ => enforce);
                    length.When(_ => enforce);
                    range.When(_ => enforce);
                    break;
                case "Unless":
                    pattern.Unless(_ => !enforce);
                    length.Unless(_ => !enforce);
                    range.Unless(_ => !enforce);
                    break;
                case "WhenAsync":
                    pattern.WhenAsync((_, _) => Task.FromResult(enforce));
                    length.WhenAsync((_, _) => Task.FromResult(enforce));
                    range.WhenAsync((_, _) => Task.FromResult(enforce));
                    break;
                case "UnlessAsync":
                    pattern.UnlessAsync((_, _) => Task.FromResult(!enforce));
                    length.UnlessAsync((_, _) => Task.FromResult(!enforce));
                    range.UnlessAsync((_, _) => Task.FromResult(!enforce));
                    break;
            }
        };

        switch (condition)
        {
            case "OuterWhen": validator.When(_ => enforce, addRules); break;
            case "OuterUnless": validator.Unless(_ => !enforce, addRules); break;
            case "OuterWhenAsync": validator.WhenAsync((_, _) => Task.FromResult(enforce), addRules); break;
            case "OuterUnlessAsync": validator.UnlessAsync((_, _) => Task.FromResult(!enforce), addRules); break;
            default: addRules(); break;
        }

        var invalid = new RuleForEachRequest { Tags = ["INVALID"], Codes = ["waytoolong"], Scores = [-1] };
        (await validator.ValidateAsync(invalid)).IsValid.ShouldBeTrue();
        enforce = true;
        (await validator.ValidateAsync(invalid)).Errors.Count.ShouldBe(4);
        (await validator.ValidateAsync(new RuleForEachRequest { Tags = ["aaa"], Codes = ["ok"], Scores = [3] })).IsValid.ShouldBeTrue();

        var schema = CreateRuleForEachSchema();
        ApplyValidatorToSchema(schema, validator);
        GetPropertySchema(schema, "tags").Items!.Pattern.ShouldBeNull();
        GetPropertySchema(schema, "codes").Items!.MinLength.ShouldBeNull();
        GetPropertySchema(schema, "codes").Items!.MaxLength.ShouldBeNull();
        GetPropertySchema(schema, "scores").Items!.Minimum.ShouldBeNull();
        GetPropertySchema(schema, "scores").Items!.Maximum.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task conditional_rule_for_each_current_validator_keeps_unconditional_components(bool asyncCondition)
    {
        var enforce = false;
        var validator = new InlineValidator<RuleForEachRequest>();
        var pattern = validator.RuleForEach(x => x.Tags).MaximumLength(30).Matches("^a+$");
        var length = validator.RuleForEach(x => x.Codes).MinimumLength(2).MaximumLength(5);
        var range = validator.RuleForEach(x => x.Scores).GreaterThanOrEqualTo(-10).InclusiveBetween(1, 5);
        if (asyncCondition)
        {
            pattern.WhenAsync((_, _) => Task.FromResult(enforce), ApplyConditionTo.CurrentValidator);
            length.WhenAsync((_, _) => Task.FromResult(enforce), ApplyConditionTo.CurrentValidator);
            range.WhenAsync((_, _) => Task.FromResult(enforce), ApplyConditionTo.CurrentValidator);
        }
        else
        {
            pattern.When(_ => enforce, ApplyConditionTo.CurrentValidator);
            length.When(_ => enforce, ApplyConditionTo.CurrentValidator);
            range.When(_ => enforce, ApplyConditionTo.CurrentValidator);
        }
        validator.RuleForEach(x => x.Codes).MaximumLength(30);
        validator.RuleFor(x => x.Tags).NotEmpty();

        var invalid = new RuleForEachRequest { Tags = ["INVALID"], Codes = ["waytoolong"], Scores = [-1] };
        (await validator.ValidateAsync(invalid)).IsValid.ShouldBeTrue();
        enforce = true;
        (await validator.ValidateAsync(invalid)).Errors.Count.ShouldBe(3);
        enforce = false;
        (await validator.ValidateAsync(new RuleForEachRequest { Tags = [new string('a', 31)], Codes = [""], Scores = [-11] })).Errors.Count.ShouldBe(3);

        var schema = CreateRuleForEachSchema();
        ApplyValidatorToSchema(schema, validator);
        var tags = GetPropertySchema(schema, "tags");
        tags.Items!.Pattern.ShouldBeNull();
        tags.Items.MaxLength.ShouldBe(30);
        tags.MinItems.ShouldBe(1);
        schema.Required!.ShouldContain("tags");
        GetPropertySchema(schema, "codes").Items!.MinLength.ShouldBe(2);
        GetPropertySchema(schema, "codes").Items!.MaxLength.ShouldBe(30);
        GetPropertySchema(schema, "scores").Items!.Minimum.ShouldBe("-10");
        GetPropertySchema(schema, "scores").Items!.Maximum.ShouldBeNull();
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task filtered_rule_for_each_preserves_nullable_items(bool asyncFilter, int schemaShape)
    {
        var validator = new InlineValidator<RuleForEachRequest>();
        var rule = validator.RuleForEach(x => x.Tags);
        if (asyncFilter)
            rule.WhereAsync(x => Task.FromResult(x is not null && x != "INVALID"));
        else
            rule.Where(x => x is not null && x != "INVALID");
        rule.NotNull().Length(2, 5).Matches("^[a-z]+$");

        (await validator.ValidateAsync(new RuleForEachRequest() { Tags = [null, "INVALID", "ok"] })).IsValid.ShouldBeTrue();
        (await validator.ValidateAsync(new RuleForEachRequest() { Tags = ["bad!"] })).IsValid.ShouldBeFalse();

        var items = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };
        var document = new OpenApiDocument
        {
            Components = new() { Schemas = new Dictionary<string, IOpenApiSchema> { ["Tag"] = items } }
        };
        var schema = CreateRuleForEachSchema();
        var array = GetPropertySchema(schema, "tags");
        array.Items = schemaShape == 0 ? items : new OpenApiSchemaReference("Tag", document);
        if (schemaShape == 2)
        {
            document.Components.Schemas["Tags"] = array;
            schema.Properties!["tags"] = new OpenApiSchema
            {
                OneOf = [new OpenApiSchema { Type = JsonSchemaType.Null }, new OpenApiSchemaReference("Tags", document)]
            };
        }

        var generation = ApplyValidatorToSchema(schema, validator, true, document: document);

        var appliedArray = GetPropertySchema(schema, "tags");
        if (schemaShape == 2)
        {
            appliedArray.OneOf![0].Type.ShouldBe(JsonSchemaType.Null);
            appliedArray = appliedArray.OneOf[1] is OpenApiSchemaReference arrayReference
                               ? (OpenApiSchema)document.Components.Schemas[arrayReference.Reference.Id!]
                               : (OpenApiSchema)appliedArray.OneOf[1];
        }
        var appliedItems = appliedArray.Items!;
        if (appliedItems is OpenApiSchemaReference reference)
            appliedItems = generation.OperationSchemaVariants.TryGetValue(reference.Reference.Id!, out var variant)
                               ? variant
                               : document.Components.Schemas[reference.Reference.Id!];
        appliedItems.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        appliedItems.MinLength.ShouldBeNull();
        appliedItems.MaxLength.ShouldBeNull();
        appliedItems.Pattern.ShouldBeNull();
        items.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        items.MinLength.ShouldBeNull();
        items.MaxLength.ShouldBeNull();
        items.Pattern.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task filtered_rule_for_each_omits_bounds_and_keeps_independent_rules(bool asyncFilter)
    {
        var validator = new InlineValidator<RuleForEachRequest>();
        var rule = validator.RuleForEach(x => x.Scores);
        if (asyncFilter)
            rule.WhereAsync(x => Task.FromResult(x > 0));
        else
            rule.Where(x => x > 0);
        rule.InclusiveBetween(1, 5);
        validator.RuleForEach(x => x.Tags).Where(x => x is not null).NotNull().MaximumLength(5);
        validator.RuleForEach(x => x.Tags).MaximumLength(30);
        validator.RuleFor(x => x.Tags).NotEmpty();

        (await validator.ValidateAsync(new RuleForEachRequest() { Tags = [null, "ok"], Scores = [0, 1, 5] })).IsValid.ShouldBeTrue();
        (await validator.ValidateAsync(new RuleForEachRequest() { Tags = ["ok"], Scores = [6] })).IsValid.ShouldBeFalse();
        var schema = CreateRuleForEachSchema();
        ApplyValidatorToSchema(schema, validator);

        var scores = GetPropertySchema(schema, "scores");
        scores.Items!.Minimum.ShouldBeNull();
        scores.Items.Maximum.ShouldBeNull();
        var tags = GetPropertySchema(schema, "tags");
        tags.Items!.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        tags.Items.MaxLength.ShouldBe(30);
        tags.MinItems.ShouldBe(1);
        schema.Required!.ShouldContain("tags");
    }

    [Fact]
    public void filtered_rule_for_each_schema_generation_only_reads_filter_metadata()
    {
        var validator = new InlineValidator<RuleForEachRequest>();
        validator.RuleForEach(x => x.Tags).Where(_ => throw new InvalidOperationException()).NotNull();
        validator.RuleForEach(x => x.Codes).WhereAsync(_ => throw new InvalidOperationException()).NotNull();
        var schema = CreateRuleForEachSchema();
        GetPropertySchema(schema, "codes").Items = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null };

        ApplyValidatorToSchema(schema, validator);

        GetPropertySchema(schema, "tags").Items!.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
        GetPropertySchema(schema, "codes").Items!.Type.ShouldBe(JsonSchemaType.String | JsonSchemaType.Null);
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

    static OpenApiGenerationState ApplyValidatorToSchema(OpenApiSchema schema,
                                                         IValidator validator,
                                                         bool localizeReferencedSchemas = false,
                                                         SharedContext? sharedCtx = null,
                                                         OpenApiDocument? document = null,
                                                         string operationKey = "GET:/test")
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
        sharedCtx ??= new();
        var generation = sharedCtx.For(document ?? new());
        using var applier = (IDisposable)Activator.CreateInstance(
            applierType,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            binder: null,
            args:
            [
                sharedCtx,
                generation,
                resolver,
                null,
                (Func<IServiceScope>)(() => resolver.CreateScope()),
                rules,
                true,
                operationKey,
                "requestBody",
                localizeReferencedSchemas
            ],
            culture: null)!;

        applierType.GetMethod("ApplyValidator", BindingFlags.Instance | BindingFlags.NonPublic)!
                   .Invoke(applier, [schema, validator, string.Empty, new HashSet<Type>()]);

        return generation;
    }

sealed class TagItemsRequest
    {
        [JsonPropertyName("tags")]
        public List<TagItem?> Tags { get; set; } = [];
    }

    sealed class TagItem
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
    }

    sealed class TagItemValidator : AbstractValidator<TagItem?>
    {
        public TagItemValidator()
        {
            RuleFor(x => x!.Name).MaximumLength(5);
        }
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

    static OpenApiSchema CreateRuleForEachSchema()
        => new()
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["tags"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null }
                },
                ["codes"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["scores"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema { Type = JsonSchemaType.Integer }
                },
                ["labels"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array | JsonSchemaType.Null,
                    Items = new OpenApiSchema { Type = JsonSchemaType.String }
                }
            }
        };

    sealed class RuleForEachRequest
    {
        [JsonPropertyName("tags")]
        public List<string?>? Tags { get; init; }

        [JsonPropertyName("codes")]
        public List<string> Codes { get; init; } = [];

        [JsonPropertyName("scores")]
        public List<int> Scores { get; init; } = [];

        [JsonPropertyName("labels")]
        public List<string>? Labels { get; init; }
    }

    sealed class RuleForEachValidator : Validator<RuleForEachRequest>
    {
        public RuleForEachValidator()
        {
            RuleForEach(x => x.Tags).NotEmpty();
            RuleForEach(x => x.Codes).MaximumLength(20);
            RuleForEach(x => x.Scores).InclusiveBetween(1, 5);
            RuleFor(x => x.Labels).NotEmpty();
            RuleForEach(x => x.Labels).MaximumLength(5);
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
