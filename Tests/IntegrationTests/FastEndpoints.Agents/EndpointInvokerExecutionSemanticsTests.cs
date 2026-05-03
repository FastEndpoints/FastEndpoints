using System.Text.Json;
using FluentValidation;
using FastEndpoints.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Agents.Tests;

public class EndpointInvokerExecutionSemanticsTests
{
    [Fact]
    public async Task Agent_invocation_exposes_synthetic_context_via_http_context_accessor()
    {
        using var provider = BuildServices(typeof(AccessorAwareEndpoint));
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var outerContext = new DefaultHttpContext { RequestServices = provider };
        outerContext.Request.Path = "/outer-transport";
        accessor.HttpContext = outerContext;

        var result = await Invoke<AccessorAwareEndpoint>(provider, new { });

        result.Status.ShouldBe(InvocationStatus.Success);
        JsonDocument.Parse(result.Body).RootElement.GetProperty("Value").GetString().ShouldBe("ok");
        provider.GetRequiredService<InvocationProbe>().AccessorMatchesEndpointContext.ShouldBeTrue();
        provider.GetRequiredService<InvocationProbe>().AccessorPathSeen.ShouldNotBe("/outer-transport");
        accessor.HttpContext.ShouldBeSameAs(outerContext);
    }

    [Fact]
    public async Task Validation_context_add_error_marks_agent_invocation_invalid()
    {
        using var provider = BuildServices(typeof(ValidationContextEndpoint));
        provider.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { RequestServices = provider };

        var result = await Invoke<ValidationContextEndpoint>(provider, new { });

        result.Status.ShouldBe(InvocationStatus.ValidationFailed);
        result.ValidationFailures.ShouldContain(f => f.ErrorMessage == "agent validation failure");
    }

    [Fact]
    public async Task To_header_response_properties_populate_synthetic_response_headers()
    {
        using var provider = BuildServices(typeof(ToHeaderEndpoint));

        var result = await Invoke<ToHeaderEndpoint>(provider, new { });

        result.Status.ShouldBe(InvocationStatus.Success);
        provider.GetRequiredService<InvocationProbe>().ResponseHeaderSeen.ShouldBe("header-value");
    }

    [Fact]
    public async Task Disposable_endpoint_instances_are_disposed_after_agent_invocation()
    {
        using var provider = BuildServices(typeof(DisposableEndpoint));

        var result = await Invoke<DisposableEndpoint>(provider, new { });

        result.Status.ShouldBe(InvocationStatus.Success);
        provider.GetRequiredService<InvocationProbe>().DisposableDisposed.ShouldBeTrue();
    }

    [Fact]
    public async Task Async_disposable_endpoint_instances_are_disposed_after_agent_invocation()
    {
        using var provider = BuildServices(typeof(AsyncDisposableEndpoint));

        var result = await Invoke<AsyncDisposableEndpoint>(provider, new { });

        result.Status.ShouldBe(InvocationStatus.Success);
        provider.GetRequiredService<InvocationProbe>().AsyncDisposableDisposed.ShouldBeTrue();
    }

    static async Task<InvocationResult> Invoke<TEndpoint>(IServiceProvider provider, object args)
    {
        var definition = provider.GetRequiredService<EndpointData>().Found.Single(d => d.EndpointType == typeof(TEndpoint));
        var invoker = provider.GetRequiredService<EndpointInvoker>();

        return await invoker.InvokeAsync(definition, JsonSerializer.SerializeToElement(args), null, CancellationToken.None);
    }

    static ServiceProvider BuildServices(params Type[] endpointTypes)
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<IHttpContextAccessor, TestHttpContextAccessor>();
        services.AddSingleton<InvocationProbe>();
        services.AddFastEndpoints(
            o =>
            {
                foreach (var endpointType in endpointTypes)
                    o.SourceGeneratorDiscoveredTypes.Add(endpointType);
            });
        services.AddMcp(o => o.ToolVisibilityFilter = static (_, _, _) => true);

        return services.BuildServiceProvider();
    }

    sealed class InvocationProbe
    {
        public bool AccessorMatchesEndpointContext { get; set; }
        public string? AccessorPathSeen { get; set; }
        public string? ResponseHeaderSeen { get; set; }
        public bool DisposableDisposed { get; set; }
        public bool AsyncDisposableDisposed { get; set; }
    }

    sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    sealed class TestRequest
    {
        public string? Value { get; set; }
    }

    sealed class TestResponse
    {
        public string? Value { get; set; }
    }

    [HttpPost("/agent-semantics/accessor")]
    sealed class AccessorAwareEndpoint(IHttpContextAccessor accessor, InvocationProbe probe) : Endpoint<TestRequest, TestResponse>
    {
        public override Task HandleAsync(TestRequest req, CancellationToken ct)
        {
            probe.AccessorMatchesEndpointContext = ReferenceEquals(accessor.HttpContext, HttpContext);
            probe.AccessorPathSeen = accessor.HttpContext?.Request.Path.Value;

            return Send.OkAsync(new() { Value = "ok" }, ct);
        }
    }

    [HttpPost("/agent-semantics/validation")]
    sealed class ValidationContextEndpoint : EndpointWithoutRequest<TestResponse>
    {
        public override Task HandleAsync(CancellationToken ct)
        {
            ValidationContext.Instance.AddError("agent validation failure");

            return Send.OkAsync(new() { Value = "ignored" }, ct);
        }
    }

    [HttpPost("/agent-semantics/to-header")]
    sealed class ToHeaderEndpoint(InvocationProbe probe) : EndpointWithoutRequest<HeaderResponse>
    {
        public override async Task HandleAsync(CancellationToken ct)
        {
            await Send.OkAsync(new() { HeaderValue = "header-value", Value = "ok" }, ct);
            probe.ResponseHeaderSeen = HttpContext.Response.Headers["x-agent-value"].ToString();
        }
    }

    sealed class HeaderResponse
    {
        [ToHeader("x-agent-value")]
        public string? HeaderValue { get; set; }

        public string? Value { get; set; }
    }

    [HttpPost("/agent-semantics/disposable")]
    sealed class DisposableEndpoint(InvocationProbe probe) : EndpointWithoutRequest<TestResponse>, IDisposable
    {
        public override Task HandleAsync(CancellationToken ct)
            => Send.OkAsync(new() { Value = "ok" }, ct);

        public void Dispose()
            => probe.DisposableDisposed = true;
    }

    [HttpPost("/agent-semantics/async-disposable")]
    sealed class AsyncDisposableEndpoint(InvocationProbe probe) : EndpointWithoutRequest<TestResponse>, IAsyncDisposable
    {
        public override Task HandleAsync(CancellationToken ct)
            => Send.OkAsync(new() { Value = "ok" }, ct);

        public ValueTask DisposeAsync()
        {
            probe.AsyncDisposableDisposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
