namespace FastEndpoints;

/// <summary>
/// result returned by a command rule after evaluating an input.
/// </summary>
public sealed record CommandRuleMatch
{
    /// <summary>
    /// rule result indicating that the rule did not match.
    /// </summary>
    public static CommandRuleMatch NoMatch { get; } = new(false, []);

    /// <summary>
    /// indicates whether the rule matched the input.
    /// </summary>
    public bool Matched { get; }

    /// <summary>
    /// commands to dispatch when the rule matched.
    /// </summary>
    public IReadOnlyList<PlannedCommand> Commands { get; }

    /// <summary>
    /// creates a rule match with no commands.
    /// </summary>
    public static CommandRuleMatch Match()
        => new(true, []);

    /// <summary>
    /// creates a rule match from commands using default planned-command options.
    /// </summary>
    /// <param name="commands">the commands to dispatch.</param>
    public static CommandRuleMatch Match(params ICommandBase[] commands)
        => CreateMatched(commands.Select(c => new PlannedCommand(c)));

    /// <summary>
    /// creates a rule match from planned commands.
    /// </summary>
    /// <param name="commands">the planned commands to dispatch.</param>
    public static CommandRuleMatch Match(params PlannedCommand[] commands)
        => CreateMatched(commands);

    internal static CommandRuleMatch From(IEnumerable<PlannedCommand> commands)
        => CreateMatched(commands);

    static CommandRuleMatch CreateMatched(IEnumerable<PlannedCommand> commands)
        => new(true, commands.ToArray());

    CommandRuleMatch(bool matched, IReadOnlyList<PlannedCommand> commands)
    {
        Matched = matched;
        Commands = commands;
    }
}
