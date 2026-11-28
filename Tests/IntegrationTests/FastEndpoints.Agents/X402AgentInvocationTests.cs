extern alias A2AAsm;
using System.Security.Claims;
using System.Text.Json;
using A2AAsm::FastEndpoints.A2A;
using FastEndpoints.Mcp;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;

namespace FastEndpoints.Agents.Tests;

public class X402AgentInvocationTests
{
    [Fact]
    public async Task Mcp_tool_call_to_paid_endpoint_fails_without_running_handler()
    {
        using var provider = BuildServices(
            s => s.AddMcp(o => o.ToolVisibilityFilter = static (_, _, _) => true),
            def => def.McpTool("paid_tool"));
        var tool = provider.GetRequiredService<EndpointMcpToolSource>().BuildTools().Single(t => t.ProtocolTool.Name == "paid_tool");
        var context = McpToolVisibilityTestsBridge.BuildCallRequestContext(provider, "paid_tool", BuildPrincipal(), matchedTool: tool);

        var result = await tool.InvokeAsync(context, CancellationToken.None);

        result.IsError.ShouldBe(true);
        ((TextContentBlock)result.Content[0]).Text.ShouldContain("Endpoint invocation failed.");
        provider.GetRequiredService<HandlerProbe>().Runs.ShouldBe(0);
    }

    [Fact]
    public async Task A2A_skill_call_to_paid_endpoint_fails_without_running_handler()
    {
        using var provider = BuildServices(
            s => s.AddA2A(o => o.SkillVisibilityFilter = static (_, _, _) => true),
            def => def.A2ASkill("paid_skill"));
        var ctx = new DefaultHttpContext
        {
            RequestServices = provider,
            User = BuildPrincipal(),
            Request = { Scheme = "http", Host = new("localhost") }
        };
        using var parameters = JsonDocument.Parse(
            """
            {
              "message": {
                "messageId": "client-message-1",
                "role": "user",
                "parts": [ { "data": { "Value": "ping" } } ]
              },
              "metadata": { "skill": "paid_skill" }
            }
            """);

        var ex = await Should.ThrowAsync<A2ARpcException>(
                     () => provider.GetRequiredService<A2ASkillDispatcher>()
                                   .DispatchAsync("SendMessage", parameters.RootElement.Clone(), ctx, CancellationToken.None));

        ex.Error.Code.ShouldBe(-32603);
        ex.Error.Message.ShouldBe("Endpoint invocation failed.");
        provider.GetRequiredService<HandlerProbe>().Runs.ShouldBe(0);
    }

    static ServiceProvider BuildServices(Action<IServiceCollection> addAgents, Action<EndpointDefinition> expose)
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddSingleton<HandlerProbe>();
        services.AddFastEndpoints([typeof(PaidEndpoint)]);
        addAgents(services);

        var provider = services.BuildServiceProvider();
        var def = provider.GetRequiredService<EndpointData>().Found.Single(d => d.EndpointType == typeof(PaidEndpoint));
        def.RequirePayment("1000", "Paid endpoint");
        expose(def);

        return provider;
    }

    static ClaimsPrincipal BuildPrincipal()
        => new(new ClaimsIdentity([new("sub", "caller")], "test"));

    sealed class HandlerProbe
    {
        public int Runs { get; set; }
    }

    [HttpPost("/paid")]
    sealed class PaidEndpoint(HandlerProbe probe) : Endpoint<PaidRequest, PaidResponse>
    {
        public override Task HandleAsync(PaidRequest req, CancellationToken ct)
        {
            probe.Runs++;

            return Send.OkAsync(new() { Value = "paid content delivered" }, ct);
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    sealed class PaidRequest
    {
        public string? Value { get; set; }
    }

    sealed class PaidResponse
    {
        public string? Value { get; set; }
    }
}
