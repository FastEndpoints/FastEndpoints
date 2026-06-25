// ReSharper disable NotAccessedPositionalProperty.Global

namespace FastEndpoints;

/// <summary>
/// result produced after evaluating an input and dispatching the planned commands.
/// </summary>
/// <param name="MatchedRuleCount">number of rules that matched the input.</param>
/// <param name="Outcomes">dispatch outcome for each planned command that was attempted.</param>
public sealed record CommandDispatchResult(int MatchedRuleCount, IReadOnlyList<CommandDispatchOutcome> Outcomes)
{
    /// <summary>
    /// indicates whether at least one rule matched the input.
    /// </summary>
    public bool HasMatches => MatchedRuleCount > 0;

    /// <summary>
    /// indicates whether at least one planned command was dispatched.
    /// </summary>
    public bool DispatchedAny => Outcomes.Count > 0;
}

/// <summary>
/// outcome of dispatching one planned command.
/// </summary>
/// <param name="Command">the command that was dispatched.</param>
/// <param name="Mode">the dispatch mode used for the command.</param>
/// <param name="Succeeded">whether dispatch completed successfully.</param>
/// <param name="TrackingId">job tracking id when the command was queued as a job.</param>
/// <param name="Exception">exception captured for a failed dispatch.</param>
public sealed record CommandDispatchOutcome(ICommandBase Command, CommandDispatchMode Mode, bool Succeeded, Guid? TrackingId = null, Exception? Exception = null)
{
    /// <summary>
    /// creates a successful dispatch outcome.
    /// </summary>
    /// <param name="command">the dispatched command.</param>
    /// <param name="mode">the dispatch mode used for the command.</param>
    /// <param name="trackingId">job tracking id when the command was queued as a job.</param>
    public static CommandDispatchOutcome Successful(ICommandBase command, CommandDispatchMode mode, Guid? trackingId = null)
        => new(command, mode, true, trackingId);

    /// <summary>
    /// creates a failed dispatch outcome.
    /// </summary>
    /// <param name="command">the command that failed dispatch.</param>
    /// <param name="mode">the dispatch mode used for the command.</param>
    /// <param name="exception">the exception produced by dispatch.</param>
    public static CommandDispatchOutcome Failed(ICommandBase command, CommandDispatchMode mode, Exception exception)
        => new(command, mode, false, Exception: exception);
}