using NativeAotChecker.Endpoints.CommandRules;

namespace NativeAotCheckerTests;

public class CommandRulesTests(App app) : TestBase<App>
{
    [Fact]
    public async Task CommandRules_Evaluate_Returns_Ordered_Matches()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<CommandRulesEvaluateEndpoint, CommandRulesRequest, CommandRulesEvaluateResponse>(
                                  new()
                                  {
                                      Scenario = "evaluate",
                                      Value = "ordered"
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HasMatches.ShouldBeTrue();
        res.MatchedRuleCount.ShouldBe(2);
        res.PlannedCommandCount.ShouldBe(2);
        res.CommandNames.ShouldBe(["first", "second"]);
        res.Modes.ShouldBe(["ExecuteNow", "QueueAsJob"]);
        res.FirstModeHasMatches.ShouldBeTrue();
        res.FirstModeMatchedRuleCount.ShouldBe(1);
        res.FirstModePlannedCommandCount.ShouldBe(1);
        res.FirstModeCommandNames.ShouldBe(["first"]);
    }

    [Fact]
    public async Task CommandRules_Dispatch_ExecuteNow_Runs_Command()
    {
        var value = $"execute-{Guid.NewGuid():N}";
        var (rsp, res, err) = await app.Client.POSTAsync<CommandRulesExecuteNowEndpoint, CommandRulesRequest, CommandRulesDispatchResponse>(
                                  new() { Value = value });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HasMatches.ShouldBeTrue();
        res.DispatchedAny.ShouldBeTrue();
        res.MatchedRuleCount.ShouldBe(1);
        res.Outcomes.Count.ShouldBe(1);
        res.Outcomes[0].CommandName.ShouldBe("execute-now");
        res.Outcomes[0].Mode.ShouldBe("ExecuteNow");
        res.Outcomes[0].Succeeded.ShouldBeTrue();
        res.Record.ShouldBe($"recorded:execute-now:{value}");
    }

    [Fact]
    public async Task CommandRules_Dispatch_QueueAsJob_Completes_And_Returns_Result()
    {
        var value = $"queue-{Guid.NewGuid():N}";
        var (rsp, res, err) = await app.Client.POSTAsync<CommandRulesQueueJobEndpoint, CommandRulesRequest, CommandRulesDispatchResponse>(
                                  new() { Value = value });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HasMatches.ShouldBeTrue();
        res.DispatchedAny.ShouldBeTrue();
        res.MatchedRuleCount.ShouldBe(1);
        res.Outcomes.Count.ShouldBe(1);
        res.Outcomes[0].CommandName.ShouldBe("queue-job");
        res.Outcomes[0].Mode.ShouldBe("QueueAsJob");
        res.Outcomes[0].Succeeded.ShouldBeTrue();
        res.Outcomes[0].TrackingId.ShouldNotBeNull();
        res.TrackingId.ShouldBe(res.Outcomes[0].TrackingId);
        res.JobResult.ShouldBe($"job:queue-job:{value}");
    }

    [Fact]
    public async Task CommandRules_NoMatch_Returns_NoOp_Plan()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<CommandRulesEvaluateEndpoint, CommandRulesRequest, CommandRulesEvaluateResponse>(
                                  new()
                                  {
                                      Scenario = "no-match",
                                      Value = "none"
                                  });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HasMatches.ShouldBeFalse();
        res.MatchedRuleCount.ShouldBe(0);
        res.PlannedCommandCount.ShouldBe(0);
        res.CommandNames.ShouldBeEmpty();
        res.Modes.ShouldBeEmpty();
        res.FirstModeHasMatches.ShouldBeFalse();
        res.FirstModeMatchedRuleCount.ShouldBe(0);
        res.FirstModePlannedCommandCount.ShouldBe(0);
        res.FirstModeCommandNames.ShouldBeEmpty();
    }

    [Fact]
    public async Task CommandRules_Unsupported_Command_Returns_Expected_Error()
    {
        var (rsp, res, err) = await app.Client.POSTAsync<CommandRulesUnsupportedEndpoint, CommandRulesRequest, CommandRulesDispatchResponse>(
                                  new() { Value = "unsupported" });

        if (!rsp.IsSuccessStatusCode)
            Assert.Fail(err);

        res.HasMatches.ShouldBeTrue();
        res.DispatchedAny.ShouldBeTrue();
        res.MatchedRuleCount.ShouldBe(1);
        res.Outcomes.Count.ShouldBe(1);
        res.Outcomes[0].CommandName.ShouldBe("unsupported");
        res.Outcomes[0].Mode.ShouldBe("ExecuteNow");
        res.Outcomes[0].Succeeded.ShouldBeFalse();
        res.Outcomes[0].ErrorReason.ShouldNotBeNull();
        res.Outcomes[0].ErrorReason!.ShouldContain("ICommand<TResult> execution is not supported by command rules yet.");
    }
}