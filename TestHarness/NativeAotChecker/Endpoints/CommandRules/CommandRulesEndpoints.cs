using System.Collections.Concurrent;

namespace NativeAotChecker.Endpoints.CommandRules;

sealed class CommandRulesRequest
{
    public string Scenario { get; set; } = "";
    public string Value { get; set; } = "";
}

sealed class CommandRulesEvaluateResponse
{
    public bool HasMatches { get; set; }
    public int MatchedRuleCount { get; set; }
    public int PlannedCommandCount { get; set; }
    public List<string> CommandNames { get; set; } = [];
    public List<string> Modes { get; set; } = [];
    public bool FirstModeHasMatches { get; set; }
    public int FirstModeMatchedRuleCount { get; set; }
    public int FirstModePlannedCommandCount { get; set; }
    public List<string> FirstModeCommandNames { get; set; } = [];
}

sealed class CommandRulesDispatchResponse
{
    public bool HasMatches { get; set; }
    public bool DispatchedAny { get; set; }
    public int MatchedRuleCount { get; set; }
    public List<CommandRulesDispatchOutcomeResponse> Outcomes { get; set; } = [];
    public string? Record { get; set; }
    public Guid? TrackingId { get; set; }
    public string? JobResult { get; set; }
}

sealed class CommandRulesDispatchOutcomeResponse
{
    public string CommandName { get; set; } = "";
    public string Mode { get; set; } = "";
    public bool Succeeded { get; set; }
    public Guid? TrackingId { get; set; }
    public string? ErrorReason { get; set; }
}

sealed class CommandRulesEvaluateEndpoint : Endpoint<CommandRulesRequest, CommandRulesEvaluateResponse>
{
    public override void Configure()
    {
        Post("command-rules/evaluate");
        AllowAnonymous();
    }

    public override async Task<CommandRulesEvaluateResponse> ExecuteAsync(CommandRulesRequest req, CancellationToken ct)
    {
        var plan = await Resolve<ICommandRuleEngine<CommandRulesRequest>>().EvaluateAsync(req, ct);
        var firstModePlan = await new DefaultCommandRuleEngine<CommandRulesRequest>(
                                    Resolve<IEnumerable<ICommandRule<CommandRulesRequest>>>(),
                                    new() { MatchMode = CommandRuleMatchMode.First })
                                .EvaluateAsync(req, ct);

        return new()
        {
            HasMatches = plan.HasMatches,
            MatchedRuleCount = plan.MatchedRuleCount,
            PlannedCommandCount = plan.Commands.Count,
            CommandNames = plan.Commands.Select(c => CommandRulesNames.For(c.Command)).ToList(),
            Modes = plan.Commands.Select(c => (c.Mode ?? CommandDispatchMode.ExecuteNow).ToString()).ToList(),
            FirstModeHasMatches = firstModePlan.HasMatches,
            FirstModeMatchedRuleCount = firstModePlan.MatchedRuleCount,
            FirstModePlannedCommandCount = firstModePlan.Commands.Count,
            FirstModeCommandNames = firstModePlan.Commands.Select(c => CommandRulesNames.For(c.Command)).ToList()
        };
    }
}

sealed class CommandRulesExecuteNowEndpoint : Endpoint<CommandRulesRequest, CommandRulesDispatchResponse>
{
    public override void Configure()
    {
        Post("command-rules/dispatch/execute-now");
        AllowAnonymous();
    }

    public override async Task<CommandRulesDispatchResponse> ExecuteAsync(CommandRulesRequest req, CancellationToken ct)
    {
        req.Scenario = CommandRulesScenarios.ExecuteNow;
        CommandRulesRecorder.Clear(req.Value);

        var result = await Resolve<ICommandDispatcher<CommandRulesRequest>>().DispatchAsync(req, ct);

        return CommandRulesResponses.From(result, record: CommandRulesRecorder.Get(req.Value));
    }
}

sealed class CommandRulesQueueJobEndpoint : Endpoint<CommandRulesRequest, CommandRulesDispatchResponse>
{
    public override void Configure()
    {
        Post("command-rules/dispatch/queue-job");
        AllowAnonymous();
    }

    public override async Task<CommandRulesDispatchResponse> ExecuteAsync(CommandRulesRequest req, CancellationToken ct)
    {
        req.Scenario = CommandRulesScenarios.QueueJob;
        var result = await Resolve<ICommandDispatcher<CommandRulesRequest>>().DispatchAsync(req, ct);
        var trackingId = result.Outcomes.SingleOrDefault()?.TrackingId;
        string? jobResult = null;

        while (!ct.IsCancellationRequested && trackingId.HasValue && jobResult is null)
        {
            jobResult = await JobTracker<RuleJobCommand>.GetJobResultAsync<string>(trackingId.Value, ct);
            if (jobResult is null)
                await Task.Delay(50, ct);
        }

        return CommandRulesResponses.From(result, trackingId: trackingId, jobResult: jobResult);
    }
}

sealed class CommandRulesUnsupportedEndpoint : Endpoint<CommandRulesRequest, CommandRulesDispatchResponse>
{
    public override void Configure()
    {
        Post("command-rules/unsupported");
        AllowAnonymous();
    }

    public override async Task<CommandRulesDispatchResponse> ExecuteAsync(CommandRulesRequest req, CancellationToken ct)
    {
        req.Scenario = CommandRulesScenarios.Unsupported;
        var result = await Resolve<ICommandDispatcher<CommandRulesRequest>>().DispatchAsync(req, ct);

        return CommandRulesResponses.From(result);
    }
}

sealed class FirstCommandRule : CommandRule<CommandRulesRequest>
{
    public override int Order => -10;

    public override bool CanHandle(CommandRulesRequest input)
        => input.Scenario == CommandRulesScenarios.Evaluate;

    public override IEnumerable<PlannedCommand> Build(CommandRulesRequest input)
    {
        yield return new(new RecordCommand { Name = "first", Value = input.Value }) { Mode = CommandDispatchMode.ExecuteNow };
    }
}

sealed class SecondCommandRule : CommandRule<CommandRulesRequest>
{
    public override int Order => 10;

    public override bool CanHandle(CommandRulesRequest input)
        => input.Scenario == CommandRulesScenarios.Evaluate;

    public override IEnumerable<PlannedCommand> Build(CommandRulesRequest input)
    {
        yield return new(new RuleJobCommand { Name = "second", Value = input.Value }) { Mode = CommandDispatchMode.QueueAsJob };
    }
}

sealed class ExecuteNowCommandRule : CommandRule<CommandRulesRequest>
{
    public override bool CanHandle(CommandRulesRequest input)
        => input.Scenario == CommandRulesScenarios.ExecuteNow;

    public override IEnumerable<PlannedCommand> Build(CommandRulesRequest input)
    {
        yield return new(new RecordCommand { Name = "execute-now", Value = input.Value }) { Mode = CommandDispatchMode.ExecuteNow };
    }
}

sealed class QueueJobCommandRule : CommandRule<CommandRulesRequest>
{
    public override bool CanHandle(CommandRulesRequest input)
        => input.Scenario == CommandRulesScenarios.QueueJob;

    public override IEnumerable<PlannedCommand> Build(CommandRulesRequest input)
    {
        yield return new(new RuleJobCommand { Name = "queue-job", Value = input.Value }) { Mode = CommandDispatchMode.QueueAsJob };
    }
}

sealed class UnsupportedCommandRule : CommandRule<CommandRulesRequest>
{
    public override bool CanHandle(CommandRulesRequest input)
        => input.Scenario == CommandRulesScenarios.Unsupported;

    public override IEnumerable<PlannedCommand> Build(CommandRulesRequest input)
    {
        yield return new(new UnsupportedRuleCommand { Name = "unsupported", Value = input.Value }) { Mode = CommandDispatchMode.ExecuteNow };
    }
}

sealed class RecordCommand : ICommand
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

sealed class RecordCommandHandler : ICommandHandler<RecordCommand>
{
    public Task ExecuteAsync(RecordCommand command, CancellationToken ct)
    {
        CommandRulesRecorder.Set(command.Value, $"recorded:{command.Name}:{command.Value}");

        return Task.CompletedTask;
    }
}

sealed class RuleJobCommand : ICommand<string>
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

sealed class RuleJobCommandHandler : ICommandHandler<RuleJobCommand, string>
{
    public Task<string> ExecuteAsync(RuleJobCommand command, CancellationToken ct)
        => Task.FromResult($"job:{command.Name}:{command.Value}");
}

sealed class UnsupportedRuleCommand : ICommand<string>
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

static class CommandRulesScenarios
{
    public const string Evaluate = "evaluate";
    public const string ExecuteNow = "execute-now";
    public const string QueueJob = "queue-job";
    public const string Unsupported = "unsupported";
}

static class CommandRulesRecorder
{
    static readonly ConcurrentDictionary<string, string> _records = new();

    public static void Set(string key, string value)
        => _records[key] = value;

    public static string? Get(string key)
        => _records.GetValueOrDefault(key);

    public static void Clear(string key)
        => _records.TryRemove(key, out _);
}

static class CommandRulesResponses
{
    public static CommandRulesDispatchResponse From(CommandDispatchResult result, string? record = null, Guid? trackingId = null, string? jobResult = null)
        => new()
        {
            HasMatches = result.HasMatches,
            DispatchedAny = result.DispatchedAny,
            MatchedRuleCount = result.MatchedRuleCount,
            Outcomes = result.Outcomes.Select(
                                 o => new CommandRulesDispatchOutcomeResponse
                                 {
                                     CommandName = CommandRulesNames.For(o.Command),
                                     Mode = o.Mode.ToString(),
                                     Succeeded = o.Succeeded,
                                     TrackingId = o.TrackingId,
                                     ErrorReason = o.Exception?.Message
                                 })
                             .ToList(),
            Record = record,
            TrackingId = trackingId,
            JobResult = jobResult
        };
}

static class CommandRulesNames
{
    public static string For(ICommandBase command)
        => command switch
        {
            RecordCommand c => c.Name,
            RuleJobCommand c => c.Name,
            UnsupportedRuleCommand c => c.Name,
            _ => command.GetType().Name
        };
}