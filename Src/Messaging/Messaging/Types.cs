// ReSharper disable InconsistentNaming

namespace FastEndpoints.Messaging;

static class Types
{
    internal static readonly Type CommandHandlerExecutorOf2 = typeof(CommandHandlerExecutor<,>);
    internal static readonly Type ICommandHandler = typeof(ICommandHandler);
    internal static readonly Type ICommandHandlerOf1 = typeof(ICommandHandler<>);
    internal static readonly Type ICommandHandlerOf2 = typeof(ICommandHandler<,>);
    internal static readonly Type IEventHandler = typeof(IEventHandler);
    internal static readonly Type IEventHandlerOf1 = typeof(IEventHandler<>);
    internal static readonly Type VoidResult = typeof(Void);
}