using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

public static class ClientExtensions
{
    //key: tCommand
    //val: remote server that has handler listening
    internal static readonly Dictionary<Type, ClientConfiguration> CommandToRemoteMap = new();

    public static IHost MapRemoteHandlers(this IHost host, string serverAddress, Action<ClientConfiguration> c)
    {
        c(new ClientConfiguration(serverAddress, host.Services));
        var logger = host.Services.GetRequiredService<ILogger<MessagingClient>>();
        logger.LogInformation(
            "Messaging client configured!\r\nServer: {address}\r\nTotal commands: {count}",
            serverAddress, CommandToRemoteMap.Count);
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

internal sealed class MessagingClient { }