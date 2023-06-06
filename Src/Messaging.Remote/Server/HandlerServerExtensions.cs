using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

/// <summary>
/// handler server extensions
/// </summary>
public static class HandlerServerExtensions
{
    /// <summary>
    /// configure the handler server which will host a collection of command handlers. this should only be called once per application.
    /// <para>
    /// IMPORTANT: specify wich handlers this server will be hosting via <see cref="HandlerServer.MapHandler{TCommand, THandler, TResult}"/> method. which is accessible in the action <paramref name="s"/>.
    /// </para>
    /// </summary>
    /// <param name="s">configuration action for the server</param>
    public static IServiceCollection AddHandlerServer(this IServiceCollection services, Action<HandlerServer> s)
    {
        var server = new HandlerServer(services);
        s(server);

        services.TryAddSingleton(server);

        return services;
    }

    /// <summary>
    /// start the handler server that was configured via <see cref="AddHandlerServer(IServiceCollection, Action{HandlerServer})"/>
    /// </summary>
    public static IHost StartHandlerServer(this IHost host)
    {
        var server = host.Services.GetRequiredService<HandlerServer>();
        server.StartServer(host.Services);
        return host;
    }
}