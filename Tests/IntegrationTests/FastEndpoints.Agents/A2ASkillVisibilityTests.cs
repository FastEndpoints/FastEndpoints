extern alias A2AAsm;

using System.Net;
using System.Security.Claims;
using System.Net.Http.Json;
using System.Text.Json;
using A2AAsm::FastEndpoints.A2A;
using FluentValidation;
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
    public async Task Anonymous_caller_cannot_see_or_invoke_opt_in_skill_by_default()
    {
        using var provider = BuildServices();
        var ctx = BuildContext(provider, authenticated: false);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);

        card.Skills.ShouldBeEmpty();

        var ex = await Should.ThrowAsync<A2ARpcException>(() => Dispatch(provider, "visible", ctx));

        ex.Error.Code.ShouldBe(-32601);
        ex.Error.Message.ShouldBe("Method not found: skill 'visible'");
    }

    [Fact]
    public async Task Authenticated_caller_can_see_and_invoke_opt_in_skill_by_default()
    {
        using var provider = BuildServices();
        var ctx = BuildContext(provider);

        var card = provider.GetRequiredService<AgentCardBuilder>().Build(ctx);

        card.Skills.Select(s => s.Id).ShouldBe(["visible", "hidden"], ignoreOrder: true);

        var result = await Dispatch(provider, "visible", ctx);
        var resultJson = JsonSerializer.SerializeToElement(result, Config.SerOpts.Options);

        resultJson.GetProperty("message").GetProperty("parts")[0].GetProperty("data").GetProperty("Value").GetString().ShouldBe("visible:ping");
    }

    [Fact]
    public async Task Always_true_visibility_filter_restores_anonymous_skill_access()
    {
        using var provider = BuildServices(visibilityFilter: (_, _, _) => true);
        var ctx = BuildContext(provider, authenticated: false);

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

    [Theory]
    [InlineData("forbidden", 403, "application/json", false, "")]
    [InlineData("not_found", 404, "application/json", true, "missing")]
    [InlineData("bad_request", 400, "application/json", true, "bad-request")]
    [InlineData("string_error", 500, "text/plain", false, "boom")]
    public async Task Http_SendMessage_maps_endpoint_non_2xx_responses_to_json_rpc_error(
        string skill,
        int expectedStatus,
        string expectedContentType,
        bool expectJsonBody,
        string expectedBodyValue)
    {
        await using var app = BuildHttpApp(
            skillFilter: def => def.EndpointType == typeof(ForbiddenSkillEndpoint) ||
                               def.EndpointType == typeof(NotFoundSkillEndpoint) ||
                               def.EndpointType == typeof(BadRequestSkillEndpoint) ||
                               def.EndpointType == typeof(StringErrorSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/a2a",
            new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "SendMessage",
                @params = new
                {
                    message = new
                    {
                        messageId = "client-message-1",
                        role = "ROLE_USER",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    },
                    metadata = new { skill }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var error = json.GetProperty("error");
        var data = error.GetProperty("data");

        json.TryGetProperty("result", out _).ShouldBeFalse();
        error.GetProperty("code").GetInt32().ShouldBe(-32000);
        error.GetProperty("message").GetString().ShouldBe($"Endpoint returned HTTP {expectedStatus}.");
        data.GetProperty("statusCode").GetInt32().ShouldBe(expectedStatus);
        data.GetProperty("contentType").GetString().ShouldBe(expectedContentType);
        var rawBody = data.GetProperty("rawBody").GetString();

        if (string.IsNullOrEmpty(expectedBodyValue))
            rawBody.ShouldBe(string.Empty);
        else
        {
            rawBody.ShouldNotBeNull();
            rawBody.ShouldContain(expectedBodyValue);
        }

        if (expectJsonBody)
        {
            data.GetProperty("body").ToString().ShouldContain(expectedBodyValue);
        }
        else
        {
            data.GetProperty("body").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Http_SendMessage_preserves_validation_failures_as_invalid_params()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(ValidationSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        var response = await client.PostAsJsonAsync(
            "/a2a",
            new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "SendMessage",
                @params = new
                {
                    message = new
                    {
                        messageId = "client-message-1",
                        role = "ROLE_USER",
                        parts = new[] { new { data = new { Value = "" } } }
                    },
                    metadata = new { skill = "validation" }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        json.TryGetProperty("result", out _).ShouldBeFalse();
        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
        json.GetProperty("error").GetProperty("message").GetString().ShouldBe("validation failed");
        var validationFailure = json.GetProperty("error").GetProperty("data")[0];
        var propertyName = validationFailure.TryGetProperty("propertyName", out var camelCaseName)
                               ? camelCaseName.GetString()
                               : validationFailure.GetProperty("PropertyName").GetString();

        propertyName.ShouldBe("Value");
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
                if (visibilityFilter is not null)
                    o.SkillVisibilityFilter = visibilityFilter;
            });

        var provider = services.BuildServiceProvider();

        foreach (var def in provider.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(VisibleSkillEndpoint))
                def.A2ASkill(visibleSkillId);
            else if (def.EndpointType == typeof(HiddenSkillEndpoint))
                def.A2ASkill(hiddenSkillId);
            else if (def.EndpointType == typeof(ForbiddenSkillEndpoint))
                def.A2ASkill("forbidden");
            else if (def.EndpointType == typeof(NotFoundSkillEndpoint))
                def.A2ASkill("not_found");
            else if (def.EndpointType == typeof(BadRequestSkillEndpoint))
                def.A2ASkill("bad_request");
            else if (def.EndpointType == typeof(StringErrorSkillEndpoint))
                def.A2ASkill("string_error");
            else if (def.EndpointType == typeof(ValidationSkillEndpoint))
                def.A2ASkill("validation");
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
                    typeof(HiddenSkillEndpoint),
                    typeof(ForbiddenSkillEndpoint),
                    typeof(NotFoundSkillEndpoint),
                    typeof(BadRequestSkillEndpoint),
                    typeof(StringErrorSkillEndpoint),
                    typeof(ValidationSkillEndpoint),
                    typeof(ValidationSkillEndpointValidator)
                ]));
        builder.Services.AddA2A(
            o =>
            {
                o.SkillFilter = skillFilter;
                o.SkillVisibilityFilter = (_, _, _) => true;
            });

        var app = builder.Build();

        foreach (var def in app.Services.GetRequiredService<EndpointData>().Found)
        {
            if (def.EndpointType == typeof(VisibleSkillEndpoint))
                def.A2ASkill("visible");
            else if (def.EndpointType == typeof(HiddenSkillEndpoint))
                def.A2ASkill("hidden");
            else if (def.EndpointType == typeof(ForbiddenSkillEndpoint))
                def.A2ASkill("forbidden");
            else if (def.EndpointType == typeof(NotFoundSkillEndpoint))
                def.A2ASkill("not_found");
            else if (def.EndpointType == typeof(BadRequestSkillEndpoint))
                def.A2ASkill("bad_request");
            else if (def.EndpointType == typeof(StringErrorSkillEndpoint))
                def.A2ASkill("string_error");
            else if (def.EndpointType == typeof(ValidationSkillEndpoint))
                def.A2ASkill("validation");
        }

        app.UseA2A(rpcPattern: rpcPattern);

        return app;
    }

    static DefaultHttpContext BuildContext(IServiceProvider provider, bool authenticated = true)
    {
        var ctx = new DefaultHttpContext
        {
            RequestServices = provider,
            User = authenticated
                       ? new(new ClaimsIdentity([new("sub", "caller")], "test"))
                       : new(new ClaimsIdentity())
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

    [HttpPost("/forbidden")]
    sealed class ForbiddenSkillEndpoint : Endpoint<SkillRequest, object?>
    {
        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
            => Send.ForbiddenAsync(ct);
    }

    [HttpPost("/not-found")]
    sealed class NotFoundSkillEndpoint : Endpoint<SkillRequest, object?>
    {
        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
            => Send.ResultAsync(TypedResults.NotFound(new SkillResponse { Value = "missing:" + req.Value }));
    }

    [HttpPost("/bad-request")]
    sealed class BadRequestSkillEndpoint : Endpoint<SkillRequest, object?>
    {
        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
            => Send.ResultAsync(TypedResults.BadRequest(new SkillResponse { Value = "bad-request" }));
    }

    [HttpPost("/string-error")]
    sealed class StringErrorSkillEndpoint : Endpoint<SkillRequest, object?>
    {
        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
            => Send.StringAsync("boom", (int)HttpStatusCode.InternalServerError, cancellation: ct);
    }

    sealed class ValidationSkillRequest
    {
        public string? Value { get; set; }
    }

    [HttpPost("/validation")]
    sealed class ValidationSkillEndpoint : Endpoint<ValidationSkillRequest, SkillResponse>
    {
        public override Task HandleAsync(ValidationSkillRequest req, CancellationToken ct)
            => Send.OkAsync(new() { Value = "validated:" + req.Value }, ct);
    }

    sealed class ValidationSkillEndpointValidator : Validator<ValidationSkillRequest>
    {
        public ValidationSkillEndpointValidator()
        {
            RuleFor(x => x.Value).NotEmpty();
        }
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
