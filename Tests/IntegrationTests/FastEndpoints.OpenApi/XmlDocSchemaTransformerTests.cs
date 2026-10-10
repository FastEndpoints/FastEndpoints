using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OpenApi;

public class XmlDocSchemaTransformerTests
{
    [Theory]
    [InlineData("home", "work", typeof(Address), "A postal address.")]
    [InlineData("billing", "support", typeof(Contact), null)]
    [InlineData("primary", "secondary", typeof(Status), "A contact status.")]
    [InlineData("current", "previous", typeof(List<Address>), null)]
    public async Task referenced_properties_keep_independent_summaries(string first, string second, Type type, string? typeSummary)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.OpenApiDocument(o =>
        {
            o.DocumentName = "v1";
            o.ShortSchemaNames = true;
        });

        await using var app = builder.Build();
        app.MapGet("/person", () => new PersonResponse());
        app.MapPost("/person", (PersonResponse response) => response);
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        for (var i = 0; i < 2; i++)
        {
            var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
            var schemas = document["components"]!["schemas"]!;
            var properties = schemas[SchemaNameGenerator.GetReferenceId(typeof(PersonResponse), true)!]!["properties"]!;
            var referenceId = SchemaNameGenerator.GetReferenceId(type, true)!;

            foreach (var name in new[] { first, second })
            {
                properties[name]!["$ref"]!.GetValue<string>().ShouldBe($"#/components/schemas/{referenceId}");
                (properties[name]!["description"]?.GetValue<string>()).ShouldBe($"The {name} property.");
            }

            (schemas[referenceId]!["description"]?.GetValue<string>()).ShouldBe(typeSummary);
            properties["undocumented"]!["$ref"]!.GetValue<string>().ShouldBe($"#/components/schemas/{SchemaNameGenerator.GetReferenceId(typeof(Address), true)}");
            properties["undocumented"]!["description"].ShouldBeNull();

            foreach (var name in new[] { "convertedPrimary", "convertedSecondary" })
            {
                properties[name]!["$ref"].ShouldBeNull();
                (properties[name]!["description"]?.GetValue<string>()).ShouldBe($"The {name} property.");
            }
            properties["name"]!["description"]!.GetValue<string>().ShouldBe("The person's name.");
            properties["name"]!["example"]!.GetValue<string>().ShouldBe("Jane");
        }
    }

    [Fact]
    public async Task missing_response_schemas_keep_inline_property_summaries()
    {
        var sharedCtx = new SharedContext();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.OpenApiDocument(o =>
        {
            o.DocumentName = "v1";
            o.ShortSchemaNames = true;
            o.ConfigureOpenApi = options => options.AddDocumentTransformer(async (document, context, ct) =>
            {
                sharedCtx.For(document).MissingSchemaTypes.TryAdd("PersonResponse", typeof(PersonResponse));
                await document.AddMissingSchemas(sharedCtx, context, ct);
            });
        });

        await using var app = builder.Build();
        app.MapGet("/ping", () => "pong");
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        for (var i = 0; i < 2; i++)
        {
            var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
            var properties = document["components"]!["schemas"]!["PersonResponse"]!["properties"]!;

            foreach (var name in new[] { "home", "work", "billing", "support", "primary", "secondary", "current", "previous" })
            {
                properties[name]!["$ref"].ShouldBeNull();
                (properties[name]!["description"]?.GetValue<string>()).ShouldBe($"The {name} property.");
            }
        }
    }

    [Fact]
    public async Task multi_line_summaries_drop_source_indentation()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.OpenApiDocument(o =>
        {
            o.DocumentName = "v1";
            o.ShortSchemaNames = true;
        });

        await using var app = builder.Build();
        app.MapGet("/notes", () => new NotesResponse());
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
        var schema = document["components"]!["schemas"]![SchemaNameGenerator.GetReferenceId(typeof(NotesResponse), true)!]!;

        schema["description"]!.GetValue<string>().ShouldBe("First line\nsecond line.\n\nSecond paragraph.");
        schema["properties"]!["text"]!["description"]!.GetValue<string>().ShouldBe("Choices:\n- one\n  - nested");
    }

    /// <summary>
    /// First line
    /// second line.
    ///
    /// Second paragraph.
    /// </summary>
    public sealed class NotesResponse
    {
        /// <summary>
        /// Choices:
        /// - one
        ///   - nested
        /// </summary>
        public string Text { get; set; } = "";
    }

    /// <summary>A postal address.</summary>
    public sealed record Address(string City);

    public sealed record Contact(string Email);

    /// <summary>A contact status.</summary>
    public enum Status { Active, Inactive }

    public sealed class PersonResponse
    {
        /// <summary>The home property.</summary>
        public Address Home { get; set; } = new("Home");

        /// <summary>The work property.</summary>
        public Address Work { get; set; } = new("Work");

        /// <summary>The billing property.</summary>
        public Contact Billing { get; set; } = new("billing@example.com");

        /// <summary>The support property.</summary>
        public Contact Support { get; set; } = new("support@example.com");

        /// <summary>The primary property.</summary>
        public Status Primary { get; set; }

        /// <summary>The secondary property.</summary>
        public Status Secondary { get; set; }

        /// <summary>The current property.</summary>
        public List<Address> Current { get; set; } = [];

        /// <summary>The previous property.</summary>
        public List<Address> Previous { get; set; } = [];

        public Address Undocumented { get; set; } = new("Undocumented");

        /// <summary>The convertedPrimary property.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<Status>))]
        public Status ConvertedPrimary { get; set; }

        /// <summary>The convertedSecondary property.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter<Status>))]
        public Status ConvertedSecondary { get; set; }

        /// <summary>The person's name.</summary>
        /// <example>Jane</example>
        public string Name { get; set; } = "Jane";
    }
}
