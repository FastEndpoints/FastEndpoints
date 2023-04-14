namespace FastEndpoints;

public interface IHandlersCacheContainer
{
    Dictionary<Type, CommandHandlerDefinition> HandlersCache { get; }
}