using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Configuration;

namespace FastEndpoints;

/// <summary>
/// represents a connection to a remote server that hosts command handlers (<see cref="ICommandHandler{TCommand, TResult}"/>)
/// <para>call <see cref="Register{TCommand, TResult}"/> method to map the commands</para>
/// </summary>
public sealed class RemoteConnection
{
    private GrpcChannel? _channel;
    private readonly Dictionary<Type, ICommandExecutor> _commandExecutorMap = new(); //key: tCommand, val: command executor wrapper

    /// <summary>
    /// grpc channel settings
    /// </summary>
    public GrpcChannelOptions ChannelOptions { get; set; } = new()
    {
        HttpHandler = new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
            EnableMultipleHttp2Connections = true
        },
        ServiceConfig = new()
        {
            MethodConfigs = { new()
            {
                Names = { MethodName.Default },
                RetryPolicy = new()
                {
                    MaxAttempts = 5, // must be <= MaxRetryAttempts
                    InitialBackoff = TimeSpan.FromSeconds(1),
                    MaxBackoff = TimeSpan.FromSeconds(5),
                    BackoffMultiplier = 1.5,
                    RetryableStatusCodes = { StatusCode.Unavailable }
                }
            }}
        },
        MaxRetryAttempts = 5
    };

    /// <summary>
    /// the address of the remote server
    /// </summary>
    public string RemoteAddress { get; init; }

    internal RemoteConnection(string address)
    {
        RemoteAddress = address;
    }

    /// <summary>
    /// register a command (<see cref="ICommand{TResult}"/>) for this remote connection where the handler for this command is hosted/located.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    public void Register<TCommand, TResult>() where TCommand : class, ICommand<TResult> where TResult : class
    {
        var tCommand = typeof(TCommand);

        RemoteConnectionExtensions.CommandToRemoteMap[tCommand] = this;

        _channel = GrpcChannel.ForAddress(RemoteAddress, ChannelOptions);
        _commandExecutorMap[tCommand] = new CommandExecutor<TCommand, TResult>(_channel);
    }

    internal Task<TResult> Execute<TResult>(ICommand<TResult> cmd, Type tCommand, CancellationToken ct) where TResult : class
    {
        if (!_commandExecutorMap.TryGetValue(tCommand, out var executor))
            throw new InvalidOperationException($"No remote handler has been mapped for the command: [{tCommand.FullName}]");

        return ((ICommandExecutor<TResult>)executor).Execute(cmd, ct);
    }
}