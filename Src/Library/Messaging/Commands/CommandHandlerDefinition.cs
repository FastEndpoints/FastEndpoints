namespace FastEndpoints;

internal sealed class CommandHandlerDefinition
{
    internal Type HandlerType { get; init; }
    internal object? HandlerExecutor { get; set; }

    internal CommandHandlerDefinition(Type handlerType)
    {
        HandlerType = handlerType;
    }
}