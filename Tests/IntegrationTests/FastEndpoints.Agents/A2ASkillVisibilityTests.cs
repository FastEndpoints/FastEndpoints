extern alias A2AAsm;

using System.Security.Claims;
using System.Text.Json;
using A2AAsm::FastEndpoints.A2A;
using Microsoft.AspNetCore.Http;

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

        resultJson.GetProperty("parts")[0].GetProperty("text").GetString().ShouldBe("{\"Value\":\"visible:ping\"}");
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

        resultJson.GetProperty("parts")[0].GetProperty("text").GetString().ShouldBe("{\"Value\":\"hidden:ping\"}");
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

        resultJson.GetProperty("parts")[0].GetProperty("text").GetString().ShouldBe("{\"Value\":\"visible:ping\"}");
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

    static DefaultHttpContext BuildContext(IServiceProvider provider)
        => new()
        {
            RequestServices = provider,
            User = new(new ClaimsIdentity([new("sub", "caller")], "test"))
        };

    static Task<object?> Dispatch(IServiceProvider provider, string skill, HttpContext ctx)
    {
        using var parameters = JsonDocument.Parse(
            $$"""
              {
                "skill": "{{skill}}",
                "message": {
                  "role": "user",
                  "parts": [
                    {
                      "kind": "data",
                      "data": { "Value": "ping" }
                    }
                  ]
                }
              }
              """);

        return provider.GetRequiredService<A2ASkillDispatcher>().DispatchAsync("message/send", parameters.RootElement.Clone(), ctx, CancellationToken.None);
    }

    [HttpPost("/visible")]
    sealed class VisibleSkillEndpoint : Endpoint<SkillRequest, SkillResponse>
    {
        public override async Task HandleAsync(SkillRequest req, CancellationToken ct)
            => await Send.OkAsync(new() { Value = "visible:" + req.Value }, ct);
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
