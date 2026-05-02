using FastEndpoints.Agents;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
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
    /// MCP server itself. REST endpoint authorization is intentionally not reused by MCP; use
    /// <see cref="McpOptions.ToolVisibilityFilter" /> for separate agent-facing visibility. anonymous callers are
    /// denied by default. tools are discovered lazily when <c>UseMcp</c> is called.
    /// </summary>
    public static IServiceCollection AddMcp(this IServiceCollection services, Action<McpOptions>? configure = null)
    {
        var options = new McpOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<EndpointInvoker>();
        services.AddSingleton<EndpointMcpToolSource>();
        services.AddMcpServer()
                .WithHttpTransport()
                .WithListToolsHandler(ListToolsAsync)
                .WithCallToolHandler(CallToolAsync);

        return services;
    }

    /// <summary>
    /// maps the MCP HTTP endpoint at <paramref name="pattern" /> (default <c>/mcp</c>) and registers
    /// every opt-in FastEndpoints endpoint as a tool. call <em>after</em> <c>UseFastEndpoints()</c> so
    /// the endpoint registry is populated. pass <paramref name="configureRoute" /> to chain conventions
    /// onto the MCP route itself (e.g. <c>route => route.RequireAuthorization()</c>).
    /// </summary>
    public static IApplicationBuilder UseMcp(this IApplicationBuilder app, string pattern = "/mcp", Action<IEndpointConventionBuilder>? configureRoute = null)
    {
        if (app is not IEndpointRouteBuilder routeBuilder)
        {
            throw new InvalidOperationException(
                "UseMcp must be called on an IApplicationBuilder that also implements IEndpointRouteBuilder (such as WebApplication). " +
                "Call UseMcp after building the WebApplication, or after UseRouting in a classic pipeline.");
        }

        var route = routeBuilder.MapMcp(pattern);
        configureRoute?.Invoke(route);

        return app;
    }

    static ValueTask<ListToolsResult> ListToolsAsync(RequestContext<ListToolsRequestParams> ctx, CancellationToken ct)
    {
        _ = ct;
        var source = ctx.Services!.GetRequiredService<EndpointMcpToolSource>();
        var (principal, httpContext) = ResolveCallerContext(ctx);

        return ValueTask.FromResult(new ListToolsResult { Tools = source.BuildVisibleProtocolTools(principal, httpContext).ToList() });
    }

    static async ValueTask<CallToolResult> CallToolAsync(RequestContext<CallToolRequestParams> ctx, CancellationToken ct)
    {
        var source = ctx.Services!.GetRequiredService<EndpointMcpToolSource>();
        var tool = source.FindTool(ctx.Params.Name);

        if (tool is null)
            throw new McpException($"Tool '{ctx.Params.Name}' is not available for the current caller.");

        return await tool.InvokeAsync(ctx, ct);
    }

    static (ClaimsPrincipal Principal, HttpContext HttpContext) ResolveCallerContext<TParams>(RequestContext<TParams> ctx)
    {
        var principal = ctx.User ?? new ClaimsPrincipal();
        var httpContext = ctx.Services?.GetService<IHttpContextAccessor>()?.HttpContext ??
                          new DefaultHttpContext { RequestServices = ctx.Services!, User = principal };

        if (!ReferenceEquals(httpContext.User, principal))
            httpContext.User = principal;

        if (httpContext.RequestServices is null)
            httpContext.RequestServices = ctx.Services!;

        return (principal, httpContext);
    }
}
