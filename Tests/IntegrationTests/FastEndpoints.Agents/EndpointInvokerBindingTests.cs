using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using FastEndpoints.Mcp;

namespace FastEndpoints.Agents.Tests;

public class EndpointInvokerBindingTests
{
    [Fact]
    public async Task Agent_invocation_binds_route_values()
    {
        using var provider = BuildServices();

        var result = await Invoke<RouteBoundEndpoint>(provider, new { customerId = 42, requestId = "abc" });

        result.Status.ShouldBe(InvocationStatus.Success, string.Join(", ", result.ValidationFailures.Select(f => $"{f.PropertyName}:{f.ErrorMessage}")));
        JsonDocument.Parse(result.Body).RootElement.GetProperty("Value").GetString().ShouldBe("route:42:abc");
    }

    [Fact]
    public async Task Agent_invocation_binds_query_values()
    {
        using var provider = BuildServices();

        var result = await Invoke<QueryBoundEndpoint>(provider, new { term = "milk", page = 3 });

        result.Status.ShouldBe(InvocationStatus.Success);
        JsonDocument.Parse(result.Body).RootElement.GetProperty("Value").GetString().ShouldBe("query:milk:3");
    }

    [Fact]
    public async Task Agent_invocation_supports_mixed_route_and_query_bound_values()
    {
        using var provider = BuildServices();

        var result = await Invoke<RouteAndQueryReaderEndpoint>(provider, new { orderId = 77, customerId = 909, bodyValue = "ok" });

        result.Status.ShouldBe(InvocationStatus.Success, string.Join(", ", result.ValidationFailures.Select(f => $"{f.PropertyName}:{f.ErrorMessage}")));
        JsonDocument.Parse(result.Body).RootElement.GetProperty("Value").GetString().ShouldBe("helpers:77:909");
    }

    [Fact]
    public async Task Agent_invocation_reports_validation_error_when_required_non_body_value_is_missing()
    {
        using var provider = BuildServices();

        var result = await Invoke<RouteBoundEndpoint>(provider, new { requestId = "abc" });

        result.Status.ShouldBe(InvocationStatus.ValidationFailed);
        result.ValidationFailures.Select(f => f.PropertyName).ShouldContain("CustomerId");
    }

    [Fact]
    public async Task Agent_invocation_accepts_endpoint_serializer_context_names_for_nested_query_values()
    {
        using var provider = BuildServices();

        var result = await Invoke<SerializerContextQueryEndpoint>(provider, new { filter = new { child_value = "ok" } });

        result.Status.ShouldBe(InvocationStatus.Success, result.Exception?.ToString() ?? string.Join(", ", result.ValidationFailures.Select(f => $"{f.PropertyName}:{f.ErrorMessage}")));
        JsonDocument.Parse(result.Body).RootElement.GetProperty("value").GetString().ShouldBe("query-context:ok");
    }

    static async Task<InvocationResult> Invoke<TEndpoint>(IServiceProvider provider, object args)
    {
        var definition = provider.GetRequiredService<EndpointData>().Found.Single(d => d.EndpointType == typeof(TEndpoint));
        var invoker = provider.GetRequiredService<EndpointInvoker>();

        return await invoker.InvokeAsync(definition, JsonSerializer.SerializeToElement(args), null, CancellationToken.None);
    }

    static ServiceProvider BuildServices()
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
                s.AddSingleton<IValidator<RouteBoundRequest>, RouteBoundRequestValidator>();
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddFastEndpoints(
            o =>
            {
                o.SourceGeneratorDiscoveredTypes.Add(typeof(RouteBoundEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(QueryBoundEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(RouteAndQueryReaderEndpoint));
                o.SourceGeneratorDiscoveredTypes.Add(typeof(SerializerContextQueryEndpoint));
            });
        services.AddMcp(o => o.ToolVisibilityFilter = static (_, _, _) => true);

        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<EndpointData>().Found.Single(d => d.EndpointType == typeof(SerializerContextQueryEndpoint)).SerializerContext =
            AgentBindingSnakeCaseJsonContext.Default;

        return provider;
    }

    [HttpGet("/agent-binding/routes/{customerId:int}")]
    sealed class RouteBoundEndpoint : Endpoint<RouteBoundRequest, BindingResponse>
    {
        public override Task HandleAsync(RouteBoundRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = $"route:{req.CustomerId}:{req.RequestId}" }, ct);
    }

    sealed class RouteBoundRequest
    {
        [RouteParam(IsRequired = true)]
        public int CustomerId { get; set; }

        public string RequestId { get; set; } = "";
    }

    sealed class RouteBoundRequestValidator : Validator<RouteBoundRequest>
    {
        public RouteBoundRequestValidator()
        {
            RuleFor(x => x.RequestId).NotEmpty();
        }
    }

    [HttpGet("/agent-binding/query")]
    sealed class QueryBoundEndpoint : Endpoint<QueryBoundRequest, BindingResponse>
    {
        public override Task HandleAsync(QueryBoundRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = $"query:{req.Term}:{req.Page}" }, ct);
    }

    sealed class QueryBoundRequest
    {
        [QueryParam]
        public string Term { get; set; } = "";

        [QueryParam]
        public int Page { get; set; }
    }

    [HttpGet("/agent-binding/helpers/{orderId:int}")]
    sealed class RouteAndQueryReaderEndpoint : Endpoint<RouteAndQueryRequest, BindingResponse>
    {
        public override Task HandleAsync(RouteAndQueryRequest req, CancellationToken ct)
            => Send.OkAsync(new()
            {
                Value = $"helpers:{req.OrderId}:{Query<int>("customerId")}"
            }, ct);
    }

    sealed class RouteAndQueryRequest
    {
        [RouteParam]
        public int OrderId { get; set; }

        [QueryParam]
        public int CustomerId { get; set; }

        public string BodyValue { get; set; } = "";
    }

    public sealed class BindingResponse
    {
        public string? Value { get; set; }
    }

    [HttpGet("/agent-binding/serializer-context-query")]
    sealed class SerializerContextQueryEndpoint : Endpoint<SerializerContextQueryRequest, BindingResponse>
    {
        public override Task HandleAsync(SerializerContextQueryRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = $"query-context:{req.Filter.ChildValue}" }, ct);
    }

    public sealed class SerializerContextQueryRequest
    {
        [FromQuery]
        public SerializerContextQueryFilter Filter { get; set; } = new();
    }

    public sealed class SerializerContextQueryFilter
    {
        public string ChildValue { get; set; } = "";
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(EndpointInvokerBindingTests.SerializerContextQueryRequest))]
[JsonSerializable(typeof(EndpointInvokerBindingTests.SerializerContextQueryFilter))]
[JsonSerializable(typeof(EndpointInvokerBindingTests.BindingResponse))]
partial class AgentBindingSnakeCaseJsonContext : JsonSerializerContext;
