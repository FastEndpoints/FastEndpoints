namespace FastEndpoints;

public sealed class CommandHandlerDefinition
{
    internal Type HandlerType { get; init; }
    internal object? HandlerExecutor { get; set; }

    public CommandHandlerDefinition(Type handlerType)
    {
        HandlerType = handlerType;
    }
}