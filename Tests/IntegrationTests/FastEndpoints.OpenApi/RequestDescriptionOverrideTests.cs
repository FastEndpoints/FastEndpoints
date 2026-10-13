using System.Text.Json.Nodes;
using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class RequestDescriptionOverrideTests
{
    [Fact]
    public async Task endpoint_overrides_take_precedence_over_reference_local_xml_summaries()
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
                o.Assemblies = [typeof(RequestDescriptionOverrideTests).Assembly];
                o.Filter = type => type == typeof(TypeSummaryEndpoint) || type == typeof(CustomSummaryEndpoint) ||
                                   type == typeof(PropertySummaryEndpoint) || type == typeof(UnchangedEndpoint) ||
                                   type == typeof(ResponseSummaryEndpoint);
            });
            builder.Services.OpenApiDocument(o =>
            {
                o.DocumentName = "v1";
                o.ShortSchemaNames = true;
            });

            await using var app = builder.Build();
            app.UseFastEndpoints();
            app.MapOpenApi();
            await app.StartAsync();
            using var client = app.GetTestClient();

            for (var i = 0; i < 2; i++)
            {
                var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
                var schemas = document["components"]!["schemas"]!;

                JsonNode Resolve(JsonNode schema)
                    => schema["$ref"] is { } reference
                           ? schemas[reference.GetValue<string>().Split('/').Last()]!
                           : schema;

                JsonNode Body(string route)
                    => document["paths"]![route]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;

                foreach (var (route, description) in new[]
                         {
                             ("/description/type", "type summary"),
                             ("/description/custom", "endpoint summary"),
                             ("/description/property", "property summary"),
                             ("/description/unchanged", "property summary")
                         })
                {
                    var properties = Resolve(Body(route))["properties"]!;
                    (properties["child"]!["description"] ?? Resolve(properties["child"]!)["description"])!
                        .GetValue<string>().ShouldBe(description);
                    properties["other"]!["$ref"]!.GetValue<string>().ShouldBe($"#/components/schemas/{SchemaNameGenerator.GetReferenceId(typeof(Child), true)}");
                    properties["other"]!["description"]!.GetValue<string>().ShouldBe("other property summary");
                }

                Body("/description/property")["$ref"]!.GetValue<string>()
                    .ShouldBe(Body("/description/unchanged")["$ref"]!.GetValue<string>());
                var requestProperties = Resolve(Body("/description/unchanged"))["properties"]!;
                requestProperties["child"]!["description"]!.GetValue<string>().ShouldBe("property summary");
                var childReference = requestProperties["child"]!["$ref"]!.GetValue<string>();
                Resolve(Body("/description/type"))["properties"]!["child"]!["$ref"]!.GetValue<string>().ShouldBe(childReference);
                Body("/description/type")["$ref"]!.GetValue<string>()
                    .ShouldNotBe(Body("/description/unchanged")["$ref"]!.GetValue<string>());
                schemas[childReference.Split('/').Last()]!["description"]!.GetValue<string>().ShouldBe("type summary");

                var response = document["paths"]!["/description/response"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
                var responseChild = Resolve(response)["properties"]!["child"]!;
                (responseChild["description"] ?? Resolve(responseChild)["description"])!.GetValue<string>().ShouldBe("endpoint summary");
            }
        }
        finally
        {
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
        }
    }

    /// <summary>type summary</summary>
    public sealed class Child
    {
        public string Name { get; set; } = "";
    }

    public sealed class Request
    {
        /// <summary>property summary</summary>
        public Child Child { get; set; } = new();

        /// <summary>other property summary</summary>
        public Child Other { get; set; } = new();
    }

    public sealed class Response
    {
        /// <summary>property summary</summary>
        public Child Child { get; set; } = new();
    }

    public sealed class ResponseSummaryEndpoint : EndpointWithoutRequest<Response>
    {
        public override void Configure()
        {
            Get("/description/response");
            AllowAnonymous();
            Summary(s => s.ResponseParam<Response>(r => r.Child, "endpoint summary"));
        }

        public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(new(), ct);
    }

    public sealed class TypeSummaryEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/description/type");
            AllowAnonymous();
            Summary(s => s.Params["child"] = "type summary");
        }

        public override Task HandleAsync(Request req, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class CustomSummaryEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/description/custom");
            AllowAnonymous();
            Summary(s => s.Params["child"] = "endpoint summary");
        }

        public override Task HandleAsync(Request req, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class PropertySummaryEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/description/property");
            AllowAnonymous();
            Summary(s => s.Params["child"] = "property summary");
        }

        public override Task HandleAsync(Request req, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class UnchangedEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/description/unchanged");
            AllowAnonymous();
        }

        public override Task HandleAsync(Request req, CancellationToken ct) => Task.CompletedTask;
    }
}
