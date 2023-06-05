using Grpc.Core;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

public static class MessagingClientExtensions
{
    //key: tCommand
    //val: remote server that has handler listening
    internal static readonly Dictionary<Type, RemoteServerConfiguration> CommandToRemoteMap = new();

    public static IHost MapRemoteHandlers(this IHost host, string serverAddress, Action<RemoteServerConfiguration> r)
    {
        r(new RemoteServerConfiguration(serverAddress, host.Services));
        return host;
    }

    public static Task<TResult> RemoteExecuteAsync<TCommand, TResult>(this TCommand command, CancellationToken ct = default)
        where TCommand : class, ICommand<TResult>
        where TResult : class
    {
        var tCommand = command.GetType();

        if (!CommandToRemoteMap.TryGetValue(tCommand, out var remote))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return remote.Execute<TCommand, TResult, Method<TCommand, TResult>>(command, tCommand, ct);
    }
}