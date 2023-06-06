using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

public sealed class ServerConfiguration
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 6000;
    public ServerCredentials Credentials { get; set; } = ServerCredentials.Insecure;

    private Server? _server;
    private readonly IServiceCollection _services;

    public ServerConfiguration(IServiceCollection services)
    {
        _services = services;
    }

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
            throw new InvalidOperationException("Please configure the messaging server first!");

        HandlerExecutorBase.ServiceProvider = provider;

        _server?.Start();
        var logger = provider.GetService<ILogger<MessagingServer>>();
        logger?.LogInformation(
            " Messaging server started!\r\n - Listening On: {scheme}{host}:{port}\r\n - Total Handlers: {count}",
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
