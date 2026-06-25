namespace FastEndpoints;

/// <summary>
/// options controlling command rules and dispatch behavior.
/// </summary>
public sealed class CommandRulesOptions
{
    /// <summary>
    /// controls whether evaluation stops after the first match or collects all matches.
    /// </summary>
    public CommandRuleMatchMode MatchMode { get; set; } = CommandRuleMatchMode.All;

    /// <summary>
    /// controls behavior when no rule matches an input.
    /// </summary>
    public UnhandledRuleBehavior UnhandledBehavior { get; set; } = UnhandledRuleBehavior.NoOp;

    /// <summary>
    /// dispatch mode used when a planned command does not specify one and no mode is forced by the dispatcher call.
    /// </summary>
    public CommandDispatchMode DefaultMode { get; set; } = CommandDispatchMode.ExecuteNow;

    /// <summary>
    /// controls whether dispatch stops or continues after a command dispatch failure.
    /// </summary>
    public CommandDispatchFailureBehavior FailureBehavior { get; set; } = CommandDispatchFailureBehavior.StopOnFirstFailure;
}

/// <summary>
/// controls how many matching rules are included in a rule plan.
/// </summary>
public enum CommandRuleMatchMode
{
    /// <summary>
    /// include all matching rules.
    /// </summary>
    All,

    /// <summary>
    /// stop evaluating after the first matching rule.
    /// </summary>
    First
}

/// <summary>
/// controls behavior when no rule matches an input.
/// </summary>
public enum UnhandledRuleBehavior
{
    /// <summary>
    /// return an empty rule plan.
    /// </summary>
    NoOp,

    /// <summary>
    /// throw a <see cref="CommandRuleNotFoundException" />.
    /// </summary>
    Throw
}

/// <summary>
/// controls how planned commands are dispatched.
/// </summary>
public enum CommandDispatchMode
{
    /// <summary>
    /// execute the command immediately.
    /// </summary>
    ExecuteNow,

    /// <summary>
    /// queue the command as a job.
    /// </summary>
    QueueAsJob
}

/// <summary>
/// controls behavior after a planned command dispatch failure.
/// </summary>
public enum CommandDispatchFailureBehavior
{
    /// <summary>
    /// stop dispatching and rethrow the failure.
    /// </summary>
    StopOnFirstFailure,

    /// <summary>
    /// capture the failure outcome and continue dispatching remaining commands.
    /// </summary>
    Continue
}
