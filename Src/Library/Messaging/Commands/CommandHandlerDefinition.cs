namespace FastEndpoints;

sealed class CommandHandlerDefinition
{
    internal Type HandlerType { get; set; }
    internal object? HandlerExecutor { get; set; }

    internal CommandHandlerDefinition(Type handlerType)
    {
        HandlerType = handlerType;
    }
}