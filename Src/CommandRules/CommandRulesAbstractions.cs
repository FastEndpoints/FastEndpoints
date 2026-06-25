namespace FastEndpoints;

/// <summary>
/// creates command rule plans for an input.
/// </summary>
/// <typeparam name="TInput">the input type to evaluate.</typeparam>
public interface ICommandRuleEngine<in TInput>
{
    /// <summary>
    /// evaluates the supplied input to zero or more commands.
    /// </summary>
    /// <param name="input">the input to evaluate.</param>
    /// <param name="ct">cancellation token.</param>
    ValueTask<CommandRulePlan> EvaluateAsync(TInput input, CancellationToken ct = default);
}

/// <summary>
/// evaluates an input and dispatches the resulting commands.
/// </summary>
/// <typeparam name="TInput">the input type to dispatch from.</typeparam>
public interface ICommandDispatcher<in TInput>
{
    /// <summary>
    /// evaluates the supplied input and dispatches commands using each planned command's mode or the configured default mode.
    /// </summary>
    /// <param name="input">the input to evaluate and dispatch.</param>
    /// <param name="ct">cancellation token.</param>
    ValueTask<CommandDispatchResult> DispatchAsync(TInput input, CancellationToken ct = default);

    /// <summary>
    /// evaluates the supplied input and dispatches all commands using the specified mode.
    /// </summary>
    /// <param name="input">the input to evaluate and dispatch.</param>
    /// <param name="mode">the dispatch mode to use for all planned commands.</param>
    /// <param name="ct">cancellation token.</param>
    ValueTask<CommandDispatchResult> DispatchAsync(TInput input, CommandDispatchMode mode, CancellationToken ct = default);
}
