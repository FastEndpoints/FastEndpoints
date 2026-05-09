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
    public void Visible_duplicate_skill_ids_are_rejected_from_agent_card()
    {
        using var provider = BuildServices(visibleSkillId: "shared", hiddenSkillId: "shared");
        var ctx = BuildContext(provider);

        var ex = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<AgentCardBuilder>().Build(ctx));

        ex.Message.ShouldContain("Duplicate A2A skill ids detected");
        ex.Message.ShouldContain("shared");
    }

    [Fact]
    public async Task Visible_duplicate_skill_ids_are_rejected_before_dispatch()
    {
        using var provider = BuildServices(visibleSkillId: "shared", hiddenSkillId: "shared");
        var ctx = BuildContext(provider);

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Dispatch(provider, "shared", ctx));

        ex.Message.ShouldContain("Duplicate A2A skill ids detected");
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
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
        message.TryGetProperty("contextId", out var contextId).ShouldBeTrue();
        contextId.GetString().ShouldNotBeNullOrWhiteSpace();
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
                      "role": "user",
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
    public async Task Http_SendMessage_rejects_unsupported_a2a_version_without_execution()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/a2a");
        request.Headers.TryAddWithoutValidation("A2A-Version", "2.0");
        request.Content = JsonContent.Create(
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
                    }
                }
            });

        var response = await client.SendAsync(request, CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        json.GetProperty("id").GetInt32().ShouldBe(1);
        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32009);
        json.TryGetProperty("result", out _).ShouldBeFalse();
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Http_SendMessage_notification_does_not_return_response()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

        var response = await client.PostAsync(
            "/a2a",
            new StringContent(
                """
                {
                  "jsonrpc": "2.0",
                  "method": "SendMessage",
                  "params": {
                    "message": {
                      "messageId": "client-message-1",
                      "role": "user",
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

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(CancellationToken.None)).ShouldBe(string.Empty);
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(1);
    }

    [Theory]
    [InlineData("[]", "batch JSON-RPC requests are not supported")]
    [InlineData("[{}]", "batch JSON-RPC requests are not supported")]
    [InlineData("123", "invalid JSON-RPC request")]
    [InlineData("{ \"jsonrpc\": \"2.0\", \"id\": { \"bad\": true }, \"method\": \"SendMessage\", \"params\": {} }", "invalid JSON-RPC id")]
    [InlineData("{ \"jsonrpc\": \"2.0\", \"id\": [1], \"method\": \"SendMessage\", \"params\": {} }", "invalid JSON-RPC id")]
    public async Task Http_rejects_invalid_json_rpc_envelopes(string payload, string expectedMessage)
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

        var response = await client.PostAsync("/a2a", new StringContent(payload, System.Text.Encoding.UTF8, "application/json"), CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32600);
        json.GetProperty("error").GetProperty("message").GetString().ShouldBe(expectedMessage);
        json.TryGetProperty("result", out _).ShouldBeFalse();
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("\"abc\"")]
    public async Task Http_accepts_valid_json_rpc_id_kinds(string id)
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        var response = await client.PostAsync(
            "/a2a",
            new StringContent(
                $$"""
                {
                  "jsonrpc": "2.0",
                  "id": {{id}},
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
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        json.GetProperty("result").GetProperty("message").GetProperty("parts")[0].GetProperty("data").GetProperty("Value").GetString().ShouldBe("visible:ping");
        json.TryGetProperty("error", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Http_SendMessage_rejects_unsupported_accepted_output_modes_before_execution()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

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
                        role = "user",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    },
                    configuration = new { acceptedOutputModes = new[] { "text/plain" } }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
        json.TryGetProperty("result", out _).ShouldBeFalse();
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Http_SendMessage_rejects_unaccepted_actual_output_mode()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(TextDeclaredJsonResponseSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref TextDeclaredJsonResponseSkillEndpoint.ExecutionCount, 0);

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
                        role = "user",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    },
                    configuration = new { acceptedOutputModes = new[] { "text/plain" } }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32602);
        json.GetProperty("error").GetProperty("message").GetString().ShouldNotBeNull().ShouldContain("not accepted");
        json.TryGetProperty("result", out _).ShouldBeFalse();
        TextDeclaredJsonResponseSkillEndpoint.ExecutionCount.ShouldBe(1);
    }

    [Fact]
    public async Task Http_SendMessage_returns_non_object_json_response_as_data_part()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(JsonArrayResponseSkillEndpoint));
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
                        role = "user",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var part = json.GetProperty("result").GetProperty("message").GetProperty("parts")[0];

        part.GetProperty("data")[0].GetInt32().ShouldBe(1);
        part.GetProperty("data")[1].GetInt32().ShouldBe(2);
        part.TryGetProperty("text", out _).ShouldBeFalse();
        part.GetProperty("mediaType").GetString().ShouldBe("application/json");
    }

    [Fact]
    public async Task Http_SendMessage_accepts_v1_role_and_preserves_context_id()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
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
                        contextId = "ctx-1",
                        role = "ROLE_USER",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var message = json.GetProperty("result").GetProperty("message");

        message.GetProperty("role").GetString().ShouldBe("ROLE_AGENT");
        message.GetProperty("contextId").GetString().ShouldBe("ctx-1");
        message.TryGetProperty("taskId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Http_SendMessage_rejects_unknown_task_id_without_execution()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

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
                        taskId = "task-1",
                        role = "ROLE_USER",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32001);
        json.TryGetProperty("result", out _).ShouldBeFalse();
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Http_SendMessage_rejects_unsupported_input_media_type_without_execution()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(VisibleSkillEndpoint));
        await app.StartAsync(CancellationToken.None);
        var client = app.GetTestClient();

        System.Threading.Interlocked.Exchange(ref VisibleSkillEndpoint.ExecutionCount, 0);

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
                        parts = new[] { new { text = "hello", mediaType = "text/plain" } }
                    }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        json.GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32005);
        json.TryGetProperty("result", out _).ShouldBeFalse();
        VisibleSkillEndpoint.ExecutionCount.ShouldBe(0);
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
                        role = "user",
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
                        role = "user",
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
            """[]""",
            """{}""",
            """{ "message": "bad" }""",
            """{ "message": { "messageId": "m1", "parts": [ { "data": { "Value": "ping" } } ] } }""",
            """{ "message": { "messageId": "m1", "role": "ROLE_AGENT", "parts": [ { "data": { "Value": "ping" } } ] } }""",
            """{ "message": { "role": "user", "parts": [ { "data": { "Value": "ping" } } ] } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": {} } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [] } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [ { } ] } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [ { "text": "{}", "data": { "Value": "ping" } } ] } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [ { "text": "not json" } ] } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [ { "data": { "Value": "ping" } } ] }, "metadata": { "skill": 123 } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [ { "data": { "Value": "ping" } } ] }, "metadata": { "skill": "" } }""",
            """{ "message": { "messageId": "m1", "role": "user", "parts": [ { "raw": "cGluZw==" } ] } }"""
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
                    typeof(JsonArrayResponseSkillEndpoint),
                    typeof(TextDeclaredJsonResponseSkillEndpoint),
                    typeof(ValidationSkillEndpoint),
                    typeof(ValidationSkillEndpointValidator),
                    typeof(FaultedSkillEndpoint)
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
            else if (def.EndpointType == typeof(JsonArrayResponseSkillEndpoint))
                def.A2ASkill("json_array");
            else if (def.EndpointType == typeof(TextDeclaredJsonResponseSkillEndpoint))
                def.A2ASkill("text_declared_json", configure: info => info.OutputModes = ["text/plain"]);
            else if (def.EndpointType == typeof(ValidationSkillEndpoint))
                def.A2ASkill("validation");
            else if (def.EndpointType == typeof(FaultedSkillEndpoint))
                def.A2ASkill("faulted");
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
                  "role": "user",
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

    [Fact]
    public async Task Http_SendMessage_returns_generic_error_for_faulted_endpoint()
    {
        await using var app = BuildHttpApp(skillFilter: def => def.EndpointType == typeof(FaultedSkillEndpoint));
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
                        role = "user",
                        parts = new[] { new { data = new { Value = "ping" } } }
                    },
                    metadata = new { skill = "faulted" }
                }
            },
            CancellationToken.None);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var error = json.GetProperty("error");

        json.TryGetProperty("result", out _).ShouldBeFalse();
        error.GetProperty("code").GetInt32().ShouldBe(-32603);
        error.GetProperty("message").GetString().ShouldBe("Endpoint invocation failed.");
        error.ToString().ShouldNotContain("faulted skill");
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

    [HttpPost("/json-array")]
    sealed class JsonArrayResponseSkillEndpoint : Endpoint<SkillRequest, object?>
    {
        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
            => Send.StringAsync("[1,2]", 200, "application/json", ct);
    }

    [HttpPost("/text-declared-json")]
    sealed class TextDeclaredJsonResponseSkillEndpoint : Endpoint<SkillRequest, SkillResponse>
    {
        public static int ExecutionCount;

        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
        {
            System.Threading.Interlocked.Increment(ref ExecutionCount);

            return Send.OkAsync(new() { Value = "json:" + req.Value }, ct);
        }
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

    [HttpPost("/faulted")]
    sealed class FaultedSkillEndpoint : Endpoint<SkillRequest, SkillResponse>
    {
        public override Task HandleAsync(SkillRequest req, CancellationToken ct)
            => throw new InvalidOperationException("faulted skill");
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
