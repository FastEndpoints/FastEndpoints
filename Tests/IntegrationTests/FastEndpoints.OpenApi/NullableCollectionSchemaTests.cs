using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace OpenApi;

public class NullableCollectionSchemaTests
{
    [Fact]
    public void nullable_array_types_are_detected_as_arrays_via_flags()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array | JsonSchemaType.Null,
            Items = new OpenApiSchema { Type = JsonSchemaType.Object }
        };

        (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array) && schema.Items is not null).ShouldBeTrue();
    }

    [Fact]
    public void cloning_a_schema_does_not_add_a_const_member()
    {
        var source = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };

        var clone = ((IOpenApiSchema)source).CloneAsConcreteSchema();

        clone.ShouldNotBeNull();
        Serialize(clone).ShouldNotContain("const");
    }

    [Fact]
    public void cloning_a_nullable_enum_preserves_null_values()
    {
        var value = JsonValue.Create("DarkBlue")!;
        var source = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Enum = [value, null!]
        };

        var clone = ((IOpenApiSchema)source).CloneAsConcreteSchema()!;

        clone.Enum!.Count.ShouldBe(2);
        clone.Enum[0].ToJsonString().ShouldBe("\"DarkBlue\"");
        clone.Enum[0].ShouldNotBeSameAs(value);
        clone.Enum[1].ShouldBeNull();
    }

    [Fact]
    public async Task nullable_references_to_schemas_with_extensions_keep_the_extensions()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.OpenApiDocument(o =>
        {
            o.DocumentName = "v1";
            o.ShortSchemaNames = true;
            o.ConfigureOpenApi = options => options.AddSchemaTransformer((schema, context, _) =>
            {
                if (context.JsonTypeInfo.Type == typeof(Status) || context.JsonTypeInfo.Type == typeof(Status?))
                {
                    schema.Extensions ??= new Dictionary<string, IOpenApiExtension>();
                    schema.Extensions["x-enumDescriptions"] = new JsonNodeExtension(new JsonObject { ["pending"] = "Not started yet." });
                }

                return Task.CompletedTask;
            });
        });

        await using var app = builder.Build();
        app.MapGet("/status", () => new StatusResponse());
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        for (var i = 0; i < 2; i++)
        {
            var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
            var status = document["components"]!["schemas"]![SchemaNameGenerator.GetReferenceId(typeof(Status), true)!]!;

            status["x-enumDescriptions"]!["pending"]!.GetValue<string>().ShouldBe("Not started yet.");
        }
    }

    public enum Status
    {
        Pending,
        Done
    }

    public sealed class StatusResponse
    {
        public Status? Current { get; set; }
    }

    static string Serialize(OpenApiSchema schema)
    {
        var writer = new StringWriter();
        schema.SerializeAsV31(new OpenApiJsonWriter(writer));

        return writer.ToString();
    }
}
