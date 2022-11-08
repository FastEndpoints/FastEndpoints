namespace FastEndpoints;

internal class CommandHandlerDefinition
{
    internal Type HandlerType { get; init; }
    internal object? HandlerWrapper { get; set; }

    internal CommandHandlerDefinition(Type handlerType)
    {
        HandlerType = handlerType;
    }
}