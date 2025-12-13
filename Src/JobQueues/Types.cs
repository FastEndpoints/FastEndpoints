// ReSharper disable InconsistentNaming

namespace FastEndpoints;

static class Types
{
    internal static readonly Type ICommandBase = typeof(ICommandBase);
    internal static readonly Type ICommandHandlerOf1 = typeof(ICommandHandler<>);
    internal static readonly Type ICommandHandlerOf2 = typeof(ICommandHandler<,>);
    internal static readonly Type IJobResultStorage = typeof(IJobResultStorage);
    internal static readonly Type IJobResultProvider = typeof(IJobResultProvider);
    internal static readonly Type JobQueueOf4 = typeof(JobQueue<,,,>);
    internal static readonly Type VoidResult = typeof(Void);
}