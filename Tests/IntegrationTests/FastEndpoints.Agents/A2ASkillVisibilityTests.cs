extern alias A2AAsm;

using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Json;
using A2AAsm::FastEndpoints.A2A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;

namespace FastEndpoints.Agents.Tests;

public class A2ASkillVisibilityTests
{
    [Fact]
    public async Task Visible_skill_appears_in_agent_card_and_can_be_invoked()
    {
        using var provider = BuildServices(HideHiddenSkill);
        var ctx = BuildContext(provider);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);

        card.Skills.Select(s => s.Id).ShouldContain("visible");

        var result = await Dispatch(provider, "visible", ctx);

        var resultJson = JsonSerializer.SerializeToElement(result, Config.SerOpts.Options);

        resultJson.GetProperty("message").GetProperty("parts")[0].GetProperty("data").GetProperty("Value").GetString().ShouldBe("visible:ping");
    }

    [Fact]
    public void Hidden_skill_is_omitted_from_agent_card()
    {
        using var provider = BuildServices(HideHiddenSkill);
        var ctx = BuildContext(provider);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);

        card.Skills.Select(s => s.Id).ShouldNotContain("hidden");
    }

    [Fact]
    public async Task Hidden_skill_cannot_be_invoked_by_id()
    {
        using var provider = BuildServices(HideHiddenSkill);
        var ctx = BuildContext(provider);

        var ex = await Should.ThrowAsync<A2ARpcException>(() => Dispatch(provider, "hidden", ctx));

        ex.Error.Code.ShouldBe(-32601);
        ex.Error.Message.ShouldBe("Method not found: skill 'hidden'");
    }

    [Fact]
    public async Task All_skills_remain_visible_and_invokable_when_visibility_filter_is_null()
    {
        using var provider = BuildServices();
        var ctx = BuildContext(provider);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);

        card.Skills.Select(s => s.Id).ShouldBe(["visible", "hidden"], ignoreOrder: true);

        var result = await Dispatch(provider, "hidden", ctx);
        var resultJson = JsonSerializer.SerializeToElement(result, Config.SerOpts.Options);

        resultJson.GetProperty("message").GetProperty("parts")[0].GetProperty("data").GetProperty("Value").GetString().ShouldBe("hidden:ping");
    }

    [Fact]
    public async Task Statically_filtered_skill_cannot_be_invoked_by_id()
    {
        using var provider = BuildServices(skillFilter: def => def.EndpointType != typeof(HiddenSkillEndpoint));
        var ctx = BuildContext(provider);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);
        card.Skills.Select(s => s.Id).ShouldNotContain("hidden");

        var ex = await Should.ThrowAsync<A2ARpcException>(() => Dispatch(provider, "hidden", ctx));

        ex.Error.Code.ShouldBe(-32601);
        ex.Error.Message.ShouldBe("Method not found: skill 'hidden'");
    }

    [Fact]
    public async Task Hidden_duplicate_skill_id_does_not_shadow_later_visible_skill()
    {
        using var provider = BuildServices(
            visibilityFilter: (def, _, _) => def.EndpointType != typeof(HiddenSkillEndpoint),
            hiddenSkillId: "shared",
            visibleSkillId: "shared");
        var ctx = BuildContext(provider);

        var result = await Dispatch(provider, "shared", ctx);
        var resultJson = JsonSerializer.SerializeToElement(result, Config.SerOpts.Options);

        resultJson.GetProperty("message").GetProperty("parts")[0].GetProperty("data").GetProperty("Value").GetString().ShouldBe("visible:ping");
    }

    [Fact]
    public void Default_agent_card_route_uses_v1_discovery_path()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddA2A();
        using var app = builder.Build();

        app.UseA2A();

        var routes = ((IEndpointRouteBuilder)app).DataSources
                                                 .SelectMany(ds => ds.Endpoints)
                                                 .OfType<RouteEndpoint>()
                                                 .Select(e => e.RoutePattern.RawText)
                                                 .ToArray();

        routes.ShouldContain("/.well-known/agent-card.json");
        routes.ShouldNotContain("/.well-known/agent.json");
    }

    [Fact]
    public void Agent_card_serializes_v1_supported_interfaces()
    {
        using var provider = BuildServices(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        var ctx = BuildContext(provider);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);
        var json = JsonSerializer.SerializeToElement(card, Config.SerOpts.Options);

        json.TryGetProperty("url", out _).ShouldBeFalse();
        json.GetProperty("supportedInterfaces")[0].GetProperty("url").GetString().ShouldBe("http://localhost/a2a");
        json.GetProperty("supportedInterfaces")[0].GetProperty("protocolBinding").GetString().ShouldBe("JSONRPC");
        json.GetProperty("supportedInterfaces")[0].GetProperty("protocolVersion").GetString().ShouldBe("1.0");
    }

    [Fact]
    public async Task SendMessage_without_skill_metadata_dispatches_when_one_skill_is_visible()
    {
        using var provider = BuildServices(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        var ctx = BuildContext(provider);

        var result = await Dispatch(provider, skill: null, ctx);
        var resultJson = JsonSerializer.SerializeToElement(result, Config.SerOpts.Options);
        var message = resultJson.GetProperty("message");
        var part = message.GetProperty("parts")[0];

        message.GetProperty("role").GetString().ShouldBe("ROLE_AGENT");
        message.TryGetProperty("messageId", out var messageId).ShouldBeTrue();
        messageId.GetString().ShouldNotBeNullOrWhiteSpace();
        part.GetProperty("data").GetProperty("Value").GetString().ShouldBe("visible:ping");
        part.GetProperty("mediaType").GetString().ShouldBe("application/json");
        part.TryGetProperty("text", out _).ShouldBeFalse();
        part.TryGetProperty("kind", out _).ShouldBeFalse();
        part.TryGetProperty("mimeType", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Http_SendMessage_omits_error_on_success_and_result_on_error()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        var success = await client.PostAsync(
            "/a2a",
            new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "id": 1,
                  "method": "SendMessage",
                  "params": {
                    "message": {
                      "messageId": "client-message-1",
                      "role": "ROLE_USER",
                      "parts": [
                        { "data": { "Value": "ping" } }
                      ]
                    }
                  }
                }
                """,
                System.Text.Encoding.UTF8,
                "application/json"),
            CancellationToken.None);
        var successJson = await success.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        successJson.GetProperty("result").GetProperty("message").GetProperty("parts")[0].GetProperty("data").GetProperty("Value").GetString().ShouldBe("visible:ping");
        successJson.TryGetProperty("error", out _).ShouldBeFalse();

        var failure = await client.PostAsJsonAsync(
            "/a2a",
            new { jsonrpc = "2.0", id = 2, method = "MissingMethod", @params = new { } },
            CancellationToken.None);
        var failureJson = await failure.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        failureJson.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32601);
        failureJson.TryGetProperty("result", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Agent_card_advertises_custom_rpc_pattern()
    {
        await using var app = BuildHttpApp(rpcPattern: "/rpc", skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        var card = await client.GetFromJsonAsync<JsonElement>("/.well-known/agent-card.json", CancellationToken.None);

        card.GetProperty("supportedInterfaces")[0].GetProperty("url").GetString().ShouldBe("http://localhost/rpc");
    }

    [Fact]
    public async Task Malformed_SendMessage_returns_invalid_params_without_executing_handler()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        var invalidParams = new[]
        {
            """{}""",
            """{ "message": { "role": "ROLE_USER", "parts": [ { "data": { "Value": "ping" } } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [ { } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [ { "text": "{}", "data": { "Value": "ping" } } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [ { "text": "not json" } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [ { "data": null } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [ { "data": ["ping"] } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_USER", "parts": [ { "raw": "cGluZw==" } ] } }"""
        };

        foreach (var invalidParam in invalidParams)
        {
            System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);
            var response = await client.PostAsync(
                "/a2a",
                new StringContent(
                    $$"""
                    {
                      "jsonrpc": "2.0",
                      "id": 1,
                      "method": "SendMessage",
                      "params": {{invalidParam}}
                    }
                    """,
                    System.Text.Encoding.UTF8,
                    "application/json"),
                CancellationToken.None);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

            json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
            json.TryGetProperty("result", out _).ShouldBeFalse();
            VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
        }
    }

    static bool HideHiddenSkill(EndpointDefinition def, ClaimsPrincipal _, HttpContext __)
        => def.EndpointType != typeof(HiddenSkillEndpoint);

    static ServiceProvider BuildServices(
        Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool>? visibilityFilter = null,
        Func<EndpointDefinition, bool>? skillFilter = null,
        string visibleSkillId = "visible",
        string hiddenSkillId = "hidden")
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
            });

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddFastEndpoints(
            o => o.SourceGeneratorDiscoveredTypes.AddRange(
                [
                    typeof(VisibleSkillEndpoint),
                    typeof(HiddenSkillEndpoint)
                ]));
        services.AddA2A(
            o =>
            {
                o.SkillFilter = skillFilter;
                o.SkillVisibilityFilter = visibilityFilter;
            });

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(VisibleSkillEndpoint))
                def.A2ASkill(visibleSkillId);
            else if (def.EndpointType == typeof(HiddenSkillEndpoint))
                def.A2ASkill(hiddenSkillId);
        }

        return provider;
    }

    static WebApplication BuildHttpApp(string rpcPattern = "/a2a", Func<EndpointDefinition, bool>? skillFilter = null)
    {
        Factory.RegisterTestServices(
            s =>
            {
                s.AddSingleton(typeof(IRequestBinder<>), typeof(RequestBinder<>));
            });

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddFastEndpoints(
            o => o.SourceGeneratorDiscoveredTypes.AddRange(
                [
                    typeof(VisibleSkillEndpoint),
                    typeof(HiddenSkillEndpoint)
                ]));
        builder.Services.AddA2A(o => o.SkillFilter = skillFilter);

        var app = builder.Build();

        foreach (var def in app.Services.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(VisibleSkillEndpoint))
                def.A2ASkill("visible");
            else if (def.EndpointType == typeof(HiddenSkillEndpoint))
                def.A2ASkill("hidden");
        }

        app.UseA2A(rpcPattern: rpcPattern);

        return app;
    }

    static DefaultHttpContext BuildContext(IServiceProvider provider)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = provider,
            User = new(new ClaimsIdentity([new("sub", "caller")], "test"))
        };

        ctx.Request.Scheme = "http";
        ctx.Request.Host = new("localhost");

        return ctx;
    }

    static Task<object?> Dispatch(IServiceProvider provider, string? skill, HttpContext ctx)
    {
        var metadata = skill is null
                           ? "{}"
                           : $$"""
                             {
                               "skill": "{{skill}}"
                             }
                             """;

        using var parameters = JsonDocument.Parse(
            $$"""
              {
                "message": {
                  "messageId": "client-message-1",
                  "role": "ROLE_USER",
                  "parts": [
                    {
                      "data": { "Value": "ping" }
                    }
                  ]
                },
                "metadata": {{metadata}}
              }
              """);

        return provider.GetRequiredService<A2ASkillDispatcher>().DispatchAsync("SendMessage", parameters.RootElement.Clone(), ctx, CancellationToken.None);
    }

    [HttpPost("/visible")]
    sealed class VisibleSkillEndpoint : Endpoint<SkillRequest, SkillResponse>
    {
        public static int ExecutionCount;

        public override async Task HandleAsync(SkillRequest req, CancellationToken ct)
        {
            System.Threading.Interlocked.Increment(ref ExecutionCount);

            await Send.OkAsync(new() { Value = "visible:" + req.Value }, ct);
        }
    }

    [HttpPost("/hidden")]
    sealed class HiddenSkillEndpoint : Endpoint<SkillRequest, SkillResponse>
    {
        public override async Task HandleAsync(SkillRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "hidden:" + req.Value }, ct);
    }

    sealed class SkillRequest
    {
        public string? Value { get; set; }
    }

    sealed class SkillResponse
    {
        public string? Value { get; set; }
    }
}
