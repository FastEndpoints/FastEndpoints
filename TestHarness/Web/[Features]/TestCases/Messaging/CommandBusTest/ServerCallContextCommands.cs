using Grpc.Core;

namespace TestCases.CommandBusTest;

public class CallContextVoidCommand : ICommand
{
    public string Name { get; set; }
}

public class CallContextVoidCommandHandler : ICommandHandler<CallContextVoidCommand>, IHasServerCallContext
{
    public ServerCallContext ServerCallContext { get; set; } = null!;

    public Task ExecuteAsync(CallContextVoidCommand cmd, CancellationToken ct)
        => ServerCallContext is null
               ? throw new InvalidOperationException("ServerCallContext was not set before handler execution!")
               : Task.CompletedTask;
}

public class CallContextUnaryCommand : ICommand<string>
{
    public string HeaderName { get; set; }
}

public class CallContextUnaryCommandHandler : ICommandHandler<CallContextUnaryCommand, string>, IHasServerCallContext
{
    public ServerCallContext ServerCallContext { get; set; } = null!;

    public Task<string> ExecuteAsync(CallContextUnaryCommand cmd, CancellationToken ct)
        => Task.FromResult(ServerCallContext?.RequestHeaders.GetValue(cmd.HeaderName) ?? "context not set");
}
