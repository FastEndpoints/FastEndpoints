using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

/// <summary>
/// represents a handler server that listens to incoming commands from remote servers
/// </summary>
public sealed class HandlerServer
{
    /// <summary>
    /// the host address to bind to. specify * to bind on any host. default value: localhost
    /// </summary>
    public string Host { get; set; } = "localhost";

    /// <summary>
    /// the port to bind to. default value: 6000
    /// </summary>
    public int Port { get; set; } = 6000;

    /// <summary>
    /// server credentials for the server to configure encryption. default is no encryption.
    /// </summary>
    public ServerCredentials Credentials { get; set; } = ServerCredentials.Insecure;

    private Server? _server;
    private readonly IServiceCollection _services;

    internal HandlerServer(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// map a command handler this server is hosting.
    /// </summary>
    /// <typeparam name="TCommand">the type of the incoming command</typeparam>
    /// <typeparam name="THandler">the type of the handler for the incoming command</typeparam>
    /// <typeparam name="TResult">the type of the result that will be returned from the handler</typeparam>
    public void MapHandler<TCommand, THandler, TResult>()
        where TCommand : class, ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TResult : class
    {
        _server ??= new Server
        {
            Ports = { new ServerPort(Host, Port, Credentials) }
        };

        _services.TryAddTransient<THandler>();

        var b = ServerServiceDefinition.CreateBuilder();

        var method = new Method<TCommand, TResult>(
            type: MethodType.Unary,
            serviceName: typeof(TCommand).FullName!,
            name: nameof(ICommandHandler<TCommand, TResult>.ExecuteAsync),
            requestMarshaller: new MsgPackMarshaller<TCommand>(),
            responseMarshaller: new MsgPackMarshaller<TResult>());

        b.AddMethod(method, HandlerExecutor<TCommand, THandler, TResult>.Execute);

        _server.Services.Add(b.Build());
    }

    internal void StartServer(IServiceProvider provider)
    {
        if (_server?.Services.Any() is not true)
            throw new InvalidOperationException("Please configure the handler server first!");

        HandlerExecutorBase.ServiceProvider = provider;

        _server?.Start();
        var logger = provider.GetService<ILogger<MessagingServer>>();
        logger?.LogInformation(
            " Handler server started!\r\n Listening On: {scheme}{host}:{port}\r\n Total Handlers: {count}",
            SecurityScheme(), Host, Port, _server?.Services.Count());
    }

    private string SecurityScheme()
    {
        if (Credentials is SslServerCredentials)
            return "https://";
        else
            return "http://";
    }
}

internal sealed class MessagingServer { }
