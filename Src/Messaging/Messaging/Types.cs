using System.Diagnostics.CodeAnalysis;

// ReSharper disable InconsistentNaming

namespace FastEndpoints.Messaging;

static class Types
{
    internal static readonly Type CommandHandlerExecutorOf2 = typeof(CommandHandlerExecutor<,>);

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    internal static readonly Type VoidCommandHandlerExecutorOf1 = typeof(VoidCommandHandlerExecutor<>);
    internal static readonly Type StreamCommandHandlerExecutorOf2 = typeof(StreamCommandHandlerExecutor<,>);
    internal static readonly Type ICommandHandler = typeof(ICommandHandler);
    internal static readonly Type ICommandHandlerOf1 = typeof(ICommandHandler<>);
    internal static readonly Type ICommandHandlerOf2 = typeof(ICommandHandler<,>);
    internal static readonly Type IStreamCommandHandlerOf2 = typeof(IStreamCommandHandler<,>);
    internal static readonly Type IEventHandler = typeof(IEventHandler);
    internal static readonly Type IEventHandlerOf1 = typeof(IEventHandler<>);
    internal static readonly Type VoidResult = typeof(Void);
}