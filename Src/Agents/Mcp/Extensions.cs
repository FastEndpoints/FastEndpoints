using System.Text.Json;
using FastEndpoints.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FastEndpoints.Mcp;

/// <summary>
/// entry-point extensions for wiring FastEndpoints-as-MCP into the ASP.NET Core host.
/// typical usage:
/// <code>
/// builder.Services
///     .AddFastEndpoints()
///     .AddFastEndpointsMcp();
/// var app = builder.Build();
/// app.UseFastEndpoints();
/// app.MapFastEndpointsMcp();
/// </code>
/// </summary>
public static class Extensions
{
    /// <summary>
    /// registers the services the MCP bridge needs: the <see cref="EndpointInvoker" /> that runs
    /// endpoints in-process, the <see cref="McpOptions" /> singleton, and the <c>ModelContextProtocol</c>
    /// MCP server itself. tools are discovered lazily when <c>MapFastEndpointsMcp</c> is called.
    /// </summary>
    public static IServiceCollection AddFastEndpointsMcp(this IServiceCollection services, Action<McpOptions>? configure = null)
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
    /// the endpoint registry is populated.
    /// </summary>
    public static IEndpointConventionBuilder MapFastEndpointsMcp(this IEndpointRouteBuilder app, string pattern = "/mcp")
    {
        var source = app.ServiceProvider.GetRequiredService<EndpointMcpToolSource>();
        var tools = source.BuildTools();
        var serverOptions = app.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelContextProtocol.Server.McpServerOptions>>().Value;
        serverOptions.Capabilities ??= new();
        serverOptions.Capabilities.Tools ??= new();
        var collection = serverOptions.Capabilities.Tools.ToolCollection ??= new ModelContextProtocol.Server.McpServerPrimitiveCollection<ModelContextProtocol.Server.McpServerTool>();
        foreach (var t in tools)
            collection.Add(t);

        return app.MapMcp(pattern);
    }
}
