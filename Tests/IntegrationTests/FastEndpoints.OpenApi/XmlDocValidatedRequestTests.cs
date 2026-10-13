using FastEndpoints;
using FastEndpoints.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class XmlDocValidatedRequestTests
{
    [Fact]
    public async Task validated_request_properties_keep_summaries_and_examples()
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
                o.Assemblies = [typeof(XmlDocValidatedRequestTests).Assembly];
                o.Filter = type => type == typeof(TicketEndpoint);
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
                var body = document["paths"]!["/validated-tickets/{id}"]!["put"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
                var properties = Resolve(document, body)["properties"]!;

                foreach (var (name, expected) in new[] { ("day", "\"2026-10-06\""), ("status", "1"), ("home", "{\"city\":\"Berlin\"}"), ("codes", "[\"A1\"]") })
                {
                    var property = properties[name]!;
                    var example = property["examples"]?[0] ?? property["example"];
                    (property["description"]?.GetValue<string>()).ShouldBe($"The {name} property.");
                    example.ShouldNotBeNull(name);
                    example.ToJsonString().ShouldBe(expected, name);
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

    public sealed class TicketRequest
    {
        public string Id { get; set; } = "";

        /// <summary>The day property.</summary>
        /// <example>2026-10-06</example>
        public DateOnly Day { get; set; }

        /// <summary>The status property.</summary>
        /// <example>1</example>
        public TicketStatus Status { get; set; }

        /// <summary>The home property.</summary>
        /// <example>{"city":"Berlin"}</example>
        public TicketAddress Home { get; set; } = new();

        /// <summary>The codes property.</summary>
        /// <example>["A1"]</example>
        public List<string>? Codes { get; set; }
    }

    public sealed class TicketAddress
    {
        public string City { get; set; } = "";
    }

    public enum TicketStatus { Open, Closed }

    public sealed class TicketValidator : Validator<TicketRequest>
    {
        public TicketValidator()
        {
            RuleFor(x => x.Day).NotEmpty();
            RuleFor(x => x.Status).IsInEnum();
            RuleFor(x => x.Home.City).NotEmpty();
            RuleFor(x => x.Codes).NotEmpty();
            RuleForEach(x => x.Codes).MaximumLength(10);
        }
    }

    public sealed class TicketEndpoint : Endpoint<TicketRequest>
    {
        public override void Configure()
        {
            Put("/validated-tickets/{id}");
            AllowAnonymous();
            Validator<TicketValidator>();
        }

        public override Task HandleAsync(TicketRequest req, CancellationToken ct)
            => Send.NoContentAsync(ct);
    }
}
