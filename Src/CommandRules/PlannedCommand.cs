namespace FastEndpoints;

/// <summary>
/// command selected by a rule, with optional dispatch settings.
/// </summary>
/// <param name="Command">the command to dispatch.</param>
public sealed record PlannedCommand(ICommandBase Command)
{
    /// <summary>
    /// dispatch mode to use for this command. when unspecified, the configured default mode is used.
    /// </summary>
    public CommandDispatchMode? Mode { get; init; }

    /// <summary>
    /// job dispatch options used when the command is queued as a job.
    /// </summary>
    public JobDispatchOptions? Job { get; init; }

    /// <summary>
    /// creates a planned command using default dispatch settings.
    /// </summary>
    /// <param name="command">the command to dispatch.</param>
    public static PlannedCommand Create(ICommandBase command)
        => new(command);
}
