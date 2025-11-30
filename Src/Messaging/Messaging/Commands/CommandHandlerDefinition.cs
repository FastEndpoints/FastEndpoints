namespace FastEndpoints;

/// <summary>
/// definition for a command handler, containing the handler type and executor.
/// </summary>
sealed class CommandHandlerDefinition
{
    internal Type HandlerType { get; set; }
    internal object? HandlerExecutor { get; set; }

    /// <summary>
    /// creates a new command handler definition for the specified handler type.
    /// </summary>
    /// <param name="handlerType">the type of the command handler</param>
    public CommandHandlerDefinition(Type handlerType)
    {
        HandlerType = handlerType;
    }
}