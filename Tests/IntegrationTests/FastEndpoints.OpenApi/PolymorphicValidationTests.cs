using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.OpenApi;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class PolymorphicValidationTests
{
    [Fact]
    public void cloned_discriminator_mapping_is_independent_and_preserves_references()
    {
        var reference = new OpenApiSchemaReference("Derived");
        IOpenApiSchema source = new OpenApiSchema
        {
            Discriminator = new OpenApiDiscriminator
            {
                PropertyName = "type",
                Mapping = new Dictionary<string, OpenApiSchemaReference>
                {
                    ["simple"] = reference,
                    ["Simple"] = reference
                }
            }
        };

        var clone = source.CloneAsConcreteSchema()!;
        var original = source.Discriminator!;
        clone.Discriminator.ShouldNotBeNull();
        clone.Discriminator.ShouldNotBeSameAs(original);
        clone.Discriminator.PropertyName.ShouldBe("type");
        clone.Discriminator.Mapping.ShouldNotBeSameAs(original.Mapping);
        clone.Discriminator.Mapping!["simple"].ShouldBeSameAs(reference);
        clone.Discriminator.Mapping["Simple"].ShouldBeSameAs(reference);

        clone.Discriminator.Mapping.Remove("simple");
        clone.Discriminator.Mapping["other"] = new OpenApiSchemaReference("Other");
        original.Mapping!.Keys.Order().ShouldBe(new[] { "Simple", "simple" }.Order());
        original.Mapping["simple"].ShouldBeSameAs(reference);
    }

    [Fact]
    public async Task polymorphic_child_validators_preserve_mappings_and_isolate_constraints()
    {
        var previousRoutePrefix = Config.EpOpts.RoutePrefix;
        var previousFilter = Config.EpOpts.Filter;
        var previousConfigurator = Config.EpOpts.Configurator;

        try
        {
            using var globalState = new HostGlobalStateScope();
            Config.EpOpts.RoutePrefix = null;
            Config.EpOpts.Filter = null;
            Config.EpOpts.Configurator = null;
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddFastEndpoints(o =>
            {
                o.DisableAutoDiscovery = true;
                o.Assemblies = [typeof(PolymorphicValidationTests).Assembly];
                o.Filter = type => type == typeof(ValidatedEndpoint) || type == typeof(EmptyValidatorEndpoint) || type == typeof(PlainEndpoint);
            });
            builder.Services.OpenApiDocument(o => o.DocumentName = "v1");

            await using var app = builder.Build();
            app.UseFastEndpoints();
            app.MapOpenApi();
            await app.StartAsync();
            using var client = app.GetTestClient();

            for (var i = 0; i < 2; i++)
            {
                var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;

                foreach (var path in new[] { "/polymorphic-validated", "/polymorphic-empty-validator", "/polymorphic-plain" })
                {
                    var body = document["paths"]![path]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
                    var field = Resolve(document, Resolve(document, body)["properties"]!["field"]!);
                    field["discriminator"]!["propertyName"]!.GetValue<string>().ShouldBe("type");
                    var mapping = field["discriminator"]!["mapping"]!["simple"]!.GetValue<string>();
                    var derived = Resolve(document, new JsonObject { ["$ref"] = mapping });
                    derived["properties"]!["name"].ShouldNotBeNull();
                    var alternatives = field["anyOf"]!;
                    var localDerived = Resolve(document, alternatives[0]!);
                    var minLength = Resolve(document, localDerived["properties"]!["label"]!)["minLength"];

                    if (path == "/polymorphic-validated")
                        minLength!.GetValue<int>().ShouldBe(3);
                    else
                        minLength.ShouldBeNull();
                }
            }
        }
        finally
        {
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
        }
    }

    static JsonNode Resolve(JsonNode document, JsonNode schema)
        => schema["$ref"]?.GetValue<string>() is { } reference
               ? document["components"]!["schemas"]![reference.Split('/')[^1]]!
               : schema;

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(Derived), "simple")]
    public abstract class Base
    {
        public string Label { get; set; } = "";
    }

    public sealed class Derived : Base
    {
        public required string Name { get; init; }
    }

    public sealed class ValidatedRequest
    {
        public Base Field { get; set; } = null!;
    }

    public sealed class EmptyValidatorRequest
    {
        public Base Field { get; set; } = null!;
    }

    public sealed class PlainRequest
    {
        public Base Field { get; set; } = null!;
    }

    public sealed class BaseValidator : AbstractValidator<Base>
    {
        public BaseValidator() => RuleFor(x => x.Label).MinimumLength(3);
    }

    public sealed class EmptyBaseValidator : AbstractValidator<Base>;

    public sealed class RequestValidator : Validator<ValidatedRequest>
    {
        public RequestValidator() => RuleFor(x => x.Field).SetValidator(new BaseValidator());
    }

    public sealed class EmptyRequestValidator : Validator<EmptyValidatorRequest>
    {
        public EmptyRequestValidator() => RuleFor(x => x.Field).SetValidator(new EmptyBaseValidator());
    }

    public sealed class ValidatedEndpoint : Endpoint<ValidatedRequest>
    {
        public override void Configure()
        {
            Post("/polymorphic-validated");
            AllowAnonymous();
            Validator<RequestValidator>();
        }

        public override Task HandleAsync(ValidatedRequest req, CancellationToken ct) => Send.NoContentAsync(ct);
    }

    public sealed class EmptyValidatorEndpoint : Endpoint<EmptyValidatorRequest>
    {
        public override void Configure()
        {
            Post("/polymorphic-empty-validator");
            AllowAnonymous();
            Validator<EmptyRequestValidator>();
        }

        public override Task HandleAsync(EmptyValidatorRequest req, CancellationToken ct) => Send.NoContentAsync(ct);
    }

    public sealed class PlainEndpoint : Endpoint<PlainRequest>
    {
        public override void Configure()
        {
            Post("/polymorphic-plain");
            AllowAnonymous();
        }

        public override Task HandleAsync(PlainRequest req, CancellationToken ct) => Send.NoContentAsync(ct);
    }
}
