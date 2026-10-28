using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using System.Text.Json.Nodes;

namespace OpenApi;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SseSchemaCollection
{
    public const string Name = "SseSchemas";
}

[Collection(SseSchemaCollection.Name)]
public class SseSchemaTests : IDisposable
{
    readonly string? _routePrefix = Config.EpOpts.RoutePrefix;
    readonly Func<EndpointDefinition, bool>? _filter = Config.EpOpts.Filter;
    readonly Action<EndpointDefinition>? _configurator = Config.EpOpts.Configurator;

    public void Dispose()
    {
        Config.EpOpts.RoutePrefix = _routePrefix;
        Config.EpOpts.Filter = _filter;
        Config.EpOpts.Configurator = _configurator;
    }

    [Theory]
    [InlineData(OpenApiSpecVersion.OpenApi3_1)]
#if NET11_0_OR_GREATER
    [InlineData(OpenApiSpecVersion.OpenApi3_2)]
#endif
    public async Task sse_payload_and_nested_component_references_resolve(OpenApiSpecVersion version)
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
            o.Assemblies = [typeof(SseSchemaTests).Assembly];
            o.Filter = type => type == typeof(SseEndpoint);
        });
        builder.Services.OpenApiDocument(o =>
        {
            o.DocumentName = "v1";
            o.ShortSchemaNames = true;
            o.ConfigureOpenApi = options => options.OpenApiVersion = version;
        });

        await using var app = builder.Build();
        app.MapFastEndpoints();
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();

        for (var generation = 0; generation < 2; generation++)
        {
            var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
            var mediaType = document["paths"]!["/sse-schema"]!["get"]!["responses"]!["200"]!["content"]!["text/event-stream"]!;
#if NET11_0_OR_GREATER
            var keyword = version == OpenApiSpecVersion.OpenApi3_2 ? "itemSchema" : "x-oai-itemSchema";
            mediaType["schema"].ShouldBeNull();
#else
            const string keyword = "schema";
#endif
            var envelope = Resolve(document, mediaType[keyword]!);
            var payloadReference = envelope["properties"]!["data"]!;
            payloadReference["$ref"].ShouldNotBeNull();
            var payload = Resolve(document, payloadReference);
            payload["properties"]!["message"]!["type"]!.GetValue<string>().ShouldBe("string");
            var detailReference = payload["properties"]!["detail"]!;
            detailReference["$ref"].ShouldNotBeNull();
            Resolve(document, detailReference)["properties"]!["value"]!["type"]!.GetValue<string>().ShouldBe("string");
        }
    }

    static JsonNode Resolve(JsonNode document, JsonNode schema)
    {
        if (schema["$ref"]?.GetValue<string>() is not { } reference)
            return schema;

        reference.ShouldStartWith("#/components/schemas/");
        var component = document["components"]?["schemas"]?[reference.Split('/')[^1]];
        component.ShouldNotBeNull($"Unresolved schema reference: {reference}");

        return component;
    }

    public record SsePayload(string Message, SseDetail Detail);
    public record SseDetail(string Value);

    public sealed class SseEndpoint : EndpointWithoutRequest<ServerSentEventsResult<SsePayload>>
    {
        public override void Configure()
        {
            Get("/sse-schema");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
            => await Send.ResultAsync(TypedResults.ServerSentEvents(
                new[] { new SsePayload("hello", new("nested")) }.ToAsyncEnumerable()));
    }
}
