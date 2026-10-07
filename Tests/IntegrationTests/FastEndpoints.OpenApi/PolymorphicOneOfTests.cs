using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;

namespace OpenApi;

// issue 1197. self-hosted, off the shared Web fixture, so UseOneOfForPolymorphism does not rewrite snapshots.
// MapFastEndpoints writes process statics, so this collection runs alone and every host restores them before returning.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PolymorphicOneOfCollection
{
    public const string Name = "PolymorphicOneOf";
}

[Collection(PolymorphicOneOfCollection.Name)]
public class PolymorphicOneOfTests
{
    [Fact]
    public async Task abstract_json_polymorphic_response_oneof_matches_discriminator_mapping()
    {
        var document = await OpenApiDocument(
            true,
            static app => app.MapGet("/errors", BadRequest<Error> () => TypedResults.BadRequest<Error>(new GenericError("Something"))));
        var schema = PolymorphicSchema(document);
        var mappingRefs = schema["discriminator"]!["mapping"]!.AsObject().Select(static pair => pair.Value!.GetValue<string>()).ToArray();
        var oneOfRefs = schema["oneOf"]!.AsArray().Select(static option => option!["$ref"]!.GetValue<string>()).ToArray();

        oneOfRefs.ShouldBe(mappingRefs, ignoreOrder: true);
        schema["anyOf"].ShouldBeNull();
        schema["example"]!["$type"]!.GetValue<string>().ShouldBe("cmd_null");
    }

    [Fact]
    public async Task produces_problem_oneof_refs_resolve_and_keep_derived_properties()
    {
        var document = await OpenApiDocument(useOneOf: true, static app => app.MapFastEndpoints(), RegisterProblemEndpoint);
        var schemas = document["components"]!["schemas"]!.AsObject();
        var schema = PolymorphicSchema(document);
        var response = document["paths"]!.AsObject().Single().Value!["get"]!["responses"]!["400"]!;
        var content = response["content"]!.AsObject();

        content.ContainsKey("application/json").ShouldBeTrue(response.ToJsonString());
        content.ContainsKey("application/problem+json").ShouldBeTrue(response.ToJsonString());

        var mappingRefs = schema["discriminator"]!["mapping"]!.AsObject().Select(static pair => pair.Value!.GetValue<string>()).ToArray();
        var oneOfRefs = schema["oneOf"]!.AsArray().Select(static option => option!["$ref"]!.GetValue<string>()).ToArray();

        oneOfRefs.ShouldBe(mappingRefs, ignoreOrder: true);
        schema["anyOf"].ShouldBeNull();

        foreach (var reference in mappingRefs.Concat(oneOfRefs).Distinct(StringComparer.Ordinal))
        {
            var key = SchemaKey(reference);
            schemas.ContainsKey(key).ShouldBeTrue($"missing {key} in [{string.Join(", ", schemas.Select(static pair => pair.Key))}] {schema.ToJsonString()}");
        }

        var generic = schemas[SchemaKey(schema["discriminator"]!["mapping"]!["generic"]!.GetValue<string>())]!;
        generic["properties"]!["message"].ShouldNotBeNull(generic.ToJsonString());
        generic["required"]!.AsArray().Select(static item => item!.GetValue<string>()).ShouldContain("message");
    }

    [Fact]
    public async Task produces_problem_without_oneof_keeps_inline_derived_properties()
    {
        var document = await OpenApiDocument(useOneOf: false, static app => app.MapFastEndpoints(), RegisterProblemEndpoint);
        var schema = PolymorphicSchema(document);
        var generic = schema["anyOf"]!.AsArray().Single(static branch => branch?["properties"]?["message"] is not null)!;

        schema["oneOf"].ShouldBeNull();
        generic["properties"]!["message"].ShouldNotBeNull(schema.ToJsonString());
        generic["required"]!.AsArray().Select(static item => item!.GetValue<string>()).ShouldContain("message");
    }

    [Fact]
    public async Task mixed_discriminator_response_oneof_keeps_discriminatorless_derived_type()
    {
        var document = await OpenApiDocument(useOneOf: true, static app => app.MapGet("/mixed", MixedBase () => new Undisc(7)));
        var schemas = document["components"]!["schemas"]!.AsObject();
        var responseSchema = ResponseSchema(document);
        var responseRef = responseSchema["$ref"]!.GetValue<string>();
        var schema = schemas[SchemaKey(responseRef)]!;
        var mappingRefs = schema["discriminator"]!["mapping"]!.AsObject().Select(static pair => pair.Value!.GetValue<string>()).ToArray();
        var oneOfRefs = schema["oneOf"]!.AsArray().Select(static option => option!["$ref"]!.GetValue<string>()).ToArray();
        var extraRefs = oneOfRefs.Except(mappingRefs, StringComparer.Ordinal).ToArray();

        schema["anyOf"].ShouldBeNull();
        mappingRefs.Length.ShouldBe(1);
        foreach (var mappingRef in mappingRefs)
            oneOfRefs.ShouldContain(mappingRef);

        extraRefs.Length.ShouldBe(1);
        var undisc = schemas[SchemaKey(extraRefs[0])]!;
        HasProperty(undisc, "code").ShouldBeTrue(undisc.ToJsonString());
        HasProperty(schemas[SchemaKey(mappingRefs[0])]!, "name").ShouldBeTrue(schema.ToJsonString());
    }

    [Fact]
    public async Task mixed_discriminator_response_without_oneof_lists_both_derived_types()
    {
        var document = await OpenApiDocument(useOneOf: false, static app => app.MapGet("/mixed", MixedBase () => new Undisc(7)));
        var schemas = document["components"]!["schemas"]!.AsObject();
        var responseSchema = ResponseSchema(document);
        var responseRef = responseSchema["$ref"]!.GetValue<string>();
        var schema = schemas[SchemaKey(responseRef)]!;
        var anyOfRefs = schema["anyOf"]!.AsArray().Select(static option => option!["$ref"]!.GetValue<string>()).ToArray();

        schema["oneOf"].ShouldBeNull();
        anyOfRefs.Length.ShouldBe(2);

        var derived = anyOfRefs.Select(reference => schemas[SchemaKey(reference)]!).ToArray();

        derived.Count(static branch => HasProperty(branch, "code")).ShouldBe(1, schema.ToJsonString());
        derived.Count(static branch => HasProperty(branch, "name")).ShouldBe(1, schema.ToJsonString());
    }

    static async Task<JsonObject> OpenApiDocument(
        bool useOneOf,
        Action<WebApplication> map,
        Action<WebApplicationBuilder>? configure = null)
    {
        var previousResolver = MainExtensions.HostServiceResolver;
        var previousSerializerConfigured = MainExtensions.SerializerConfigured;
        var previousSerializerOptions = Config.SerOpts.Options;
        var previousNameResolver = ValidatorOptions.Global.PropertyNameResolver;

        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            configure?.Invoke(builder);
            builder.Services.OpenApiDocument(
                o =>
                {
                    o.DocumentName = "v1";
                    o.UseOneOfForPolymorphism = useOneOf;
                });

            await using var app = builder.Build();
            map(app);
            app.MapOpenApi();
            await app.StartAsync();

            using var client = app.GetTestClient();
            using var response = await client.GetAsync("/openapi/v1.json");
            var json = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.ShouldBeTrue(json);

            return JsonNode.Parse(json)!.AsObject();
        }
        finally
        {
            Config.SerOpts.Options = previousSerializerOptions;
            MainExtensions.SerializerConfigured = previousSerializerConfigured;
            ValidatorOptions.Global.PropertyNameResolver = previousNameResolver;
            MainExtensions.HostServiceResolver = previousResolver;
        }
    }

    static void RegisterProblemEndpoint(WebApplicationBuilder builder)
        => builder.Services.AddFastEndpoints(
            o =>
            {
                o.DisableAutoDiscovery = true;
                o.Assemblies = [typeof(PolymorphicOneOfTests).Assembly];
                o.Filter = static type => type == typeof(PolymorphicProblemEndpoint);
            });

    static JsonObject ResponseSchema(JsonObject document)
    {
        var operation = document["paths"]!.AsObject().Single().Value!["get"]!;
        var content = operation["responses"]!["200"]!["content"]!.AsObject();
        var media = content.Single(static pair => pair.Key.StartsWith("application/json", StringComparison.Ordinal)).Value!;

        return media["schema"]!.AsObject();
    }

    static bool HasProperty(JsonNode? node, string camelName)
    {
        var pascalName = char.ToUpperInvariant(camelName[0]) + camelName[1..];

        switch (node)
        {
            case JsonObject obj:
                if (obj["properties"] is JsonObject properties &&
                    (properties.ContainsKey(camelName) || properties.ContainsKey(pascalName)))
                    return true;

                foreach (var property in obj)
                {
                    if (HasProperty(property.Value, camelName))
                        return true;
                }

                return false;
            case JsonArray array:
                foreach (var item in array)
                {
                    if (HasProperty(item, camelName))
                        return true;
                }

                return false;
            default:
                return false;
        }
    }

    static JsonObject PolymorphicSchema(JsonObject document)
    {
        var found = new List<JsonObject>();
        Collect(document, found);
        found.Count.ShouldBe(1, document.ToJsonString());

        return found[0];
    }

    static void Collect(JsonNode? node, List<JsonObject> found)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["discriminator"]?["mapping"] is JsonObject)
                    found.Add(obj);

                foreach (var property in obj)
                    Collect(property.Value, found);

                break;
            case JsonArray array:
                foreach (var item in array)
                    Collect(item, found);

                break;
        }
    }

    static string SchemaKey(string reference)
    {
        const string prefix = "#/components/schemas/";
        reference.StartsWith(prefix, StringComparison.Ordinal).ShouldBeTrue(reference);

        return Uri.UnescapeDataString(reference[prefix.Length..]);
    }

    sealed class PolymorphicProblemEndpoint : EndpointWithoutRequest<BadRequest<Error>>
    {
        public override void Configure()
        {
            Get("/errors");
            AllowAnonymous();
            Description(b => b.ProducesProblem(400));
        }

        public override Task<BadRequest<Error>> ExecuteAsync(CancellationToken ct)
            => Task.FromResult<BadRequest<Error>>(TypedResults.BadRequest<Error>(new GenericError("Something")));
    }

    [JsonPolymorphic]
    [JsonDerivedType(typeof(RequestNullError), "cmd_null")]
    [JsonDerivedType(typeof(GenericError), "generic")]
    public abstract record Error;

    public sealed record RequestNullError : Error;

    public sealed record GenericError(string Message) : Error;

    [JsonPolymorphic]
    [JsonDerivedType(typeof(Disc), "disc")]
    [JsonDerivedType(typeof(Undisc))]
    public abstract record MixedBase;

    public sealed record Disc(string Name) : MixedBase;

    public sealed record Undisc(int Code) : MixedBase;
}
