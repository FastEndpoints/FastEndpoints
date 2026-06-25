namespace FastEndpoints;

/// <summary>
/// rule plan produced by a command engine before dispatch.
/// </summary>
/// <param name="MatchedRuleCount">number of rules that matched the input.</param>
/// <param name="Commands">planned commands selected for dispatch.</param>
public sealed record CommandRulePlan(int MatchedRuleCount, IReadOnlyList<PlannedCommand> Commands)
{
    /// <summary>
    /// indicates whether at least one rule matched the input.
    /// </summary>
    public bool HasMatches => MatchedRuleCount > 0;

    /// <summary>
    /// an empty rule plan with no matches and no commands.
    /// </summary>
    public static CommandRulePlan Empty { get; } = new(0, []);
}