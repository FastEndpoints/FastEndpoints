namespace FastEndpoints;

/// <summary>
/// base class for simple synchronous command rules.
/// </summary>
/// <typeparam name="TInput">the input type the rule evaluates.</typeparam>
public abstract class CommandRule<TInput> : ICommandRule<TInput>, IOrderedCommandRule
{
    /// <summary>
    /// rule ordering value. lower values are evaluated first.
    /// </summary>
    public virtual int Order => 0;

    /// <summary>
    /// determines whether this rule can handle the supplied input.
    /// </summary>
    /// <param name="input">the input to evaluate.</param>
    public abstract bool CanHandle(TInput input);

    /// <summary>
    /// builds the commands to dispatch when this rule matches.
    /// </summary>
    /// <param name="input">the input used to build planned commands.</param>
    public abstract IEnumerable<PlannedCommand> Build(TInput input);

    /// <summary>
    /// evaluates the input and returns the matching planned commands.
    /// </summary>
    /// <param name="input">the input to evaluate.</param>
    /// <param name="ct">cancellation token.</param>
    public virtual ValueTask<CommandRuleMatch> EvaluateAsync(TInput input, CancellationToken ct = default)
        => CanHandle(input)
               ? ValueTask.FromResult(CommandRuleMatch.From(Build(input)))
               : ValueTask.FromResult(CommandRuleMatch.NoMatch);
}
