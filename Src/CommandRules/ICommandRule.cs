namespace FastEndpoints;

/// <summary>
/// rule that maps an input to zero or more commands.
/// </summary>
/// <typeparam name="TInput">the input type evaluated by the rule.</typeparam>
public interface ICommandRule<in TInput>
{
    // ReSharper disable once UnusedParameter.Global
    /// <summary>
    /// evaluates the input and returns the matching planned commands.
    /// </summary>
    /// <param name="input">the input to evaluate.</param>
    /// <param name="ct">cancellation token.</param>
    ValueTask<CommandRuleMatch> EvaluateAsync(TInput input, CancellationToken ct = default);
}