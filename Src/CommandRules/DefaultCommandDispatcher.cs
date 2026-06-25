namespace FastEndpoints;

/// <summary>
/// default command dispatcher that evaluates an input and dispatches the resulting commands.
/// </summary>
/// <typeparam name="TInput">the input type to dispatch from.</typeparam>
public sealed class DefaultCommandDispatcher<TInput> : ICommandDispatcher<TInput>
{
    readonly ICommandRuleEngine<TInput> _engine;
    readonly CommandRulesOptions _options;

    /// <summary>
    /// initializes a new default command dispatcher.
    /// </summary>
    /// <param name="engine">engine used to create the rule plan.</param>
    /// <param name="options">command rule options.</param>
    public DefaultCommandDispatcher(ICommandRuleEngine<TInput> engine, CommandRulesOptions options)
    {
        _engine = engine;
        _options = options;
    }

    /// <summary>
    /// evaluates the supplied input and dispatches commands using each planned command's mode or the configured default mode.
    /// </summary>
    /// <param name="input">the input to evaluate and dispatch.</param>
    /// <param name="ct">cancellation token.</param>
    public ValueTask<CommandDispatchResult> DispatchAsync(TInput input, CancellationToken ct = default)
        => DispatchCoreAsync(input, null, ct);

    /// <summary>
    /// evaluates the supplied input and dispatches all commands using the specified mode.
    /// </summary>
    /// <param name="input">the input to evaluate and dispatch.</param>
    /// <param name="mode">the dispatch mode to use for all planned commands.</param>
    /// <param name="ct">cancellation token.</param>
    public ValueTask<CommandDispatchResult> DispatchAsync(TInput input, CommandDispatchMode mode, CancellationToken ct = default)
        => DispatchCoreAsync(input, mode, ct);

    async ValueTask<CommandDispatchResult> DispatchCoreAsync(TInput input, CommandDispatchMode? forcedMode, CancellationToken ct)
    {
        var plan = await _engine.EvaluateAsync(input, ct);
        CommandRuleValidation.ValidateRulePlan(plan);

        var outcomes = new List<CommandDispatchOutcome>(plan.Commands.Count);

        foreach (var planned in plan.Commands)
        {
            var mode = forcedMode ?? planned.Mode ?? _options.DefaultMode;

            try
            {
                outcomes.Add(await PlannedCommandExecutor.DispatchAsync(planned, mode, ct));
            }
            catch (Exception ex) when (_options.FailureBehavior == CommandDispatchFailureBehavior.Continue && ex is not OperationCanceledException)
            {
                outcomes.Add(CommandDispatchOutcome.Failed(planned.Command, mode, ex));
            }
        }

        return new(plan.MatchedRuleCount, outcomes.ToArray());
    }
}