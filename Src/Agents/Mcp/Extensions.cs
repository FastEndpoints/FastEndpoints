using FastEndpoints.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace FastEndpoints.Mcp;

/// <summary>
/// entry-point extensions for wiring FastEndpoints-as-MCP into the ASP.NET Core host.
/// typical usage:
/// <code>
/// builder.Services
///     .AddFastEndpoints()
///     .AddMcp();
/// var app = builder.Build();
/// app.UseFastEndpoints()
///    .UseMcp();
/// </code>
/// </summary>
public static class Extensions
{
    /// <summary>
    /// registers the services the MCP bridge needs: the <see cref="EndpointInvoker" /> that runs
    /// endpoints in-process, the <see cref="McpOptions" /> singleton, and the <c>ModelContextProtocol</c>
    /// MCP server itself. tools are discovered lazily when <c>UseMcp</c> is called.
    /// </summary>
    public static IServiceCollection AddMcp(this IServiceCollection services, Action<McpOptions>? configure = null)
    {
        var options = new McpOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<EndpointInvoker>();
        services.AddSingleton<EndpointMcpToolSource>();

        services.AddMcpServer()
                .WithHttpTransport();

        return services;
    }

    /// <summary>
    /// maps the MCP HTTP endpoint at <paramref name="pattern" /> (default <c>/mcp</c>) and registers
    /// every opt-in FastEndpoints endpoint as a tool. call <em>after</em> <c>UseFastEndpoints()</c> so
    /// the endpoint registry is populated. pass <paramref name="configureRoute" /> to chain conventions
    /// onto the MCP route itself (e.g. <c>route => route.RequireAuthorization()</c>).
    /// </summary>
    public static IApplicationBuilder UseMcp(
        this IApplicationBuilder app,
        string pattern = "/mcp",
        Action<IEndpointConventionBuilder>? configureRoute = null)
    {
        var source = app.ApplicationServices.GetRequiredService<EndpointMcpToolSource>();
        var tools = source.BuildTools();
        var serverOptions = app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Options.IOptions<McpServerOptions>>().Value;
        serverOptions.ToolCollection ??= new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var t in tools)
            serverOptions.ToolCollection.Add(t);

        if (app is not IEndpointRouteBuilder routeBuilder)
            throw new InvalidOperationException(
                "UseMcp must be called on an IApplicationBuilder that also implements IEndpointRouteBuilder (such as WebApplication). " +
                "Call UseMcp after building the WebApplication, or after UseRouting in a classic pipeline.");

        var route = routeBuilder.MapMcp(pattern);
        configureRoute?.Invoke(route);

        return app;
    }
}
