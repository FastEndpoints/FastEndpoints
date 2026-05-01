using System.Text.Json;
using FastEndpoints.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.A2A;

/// <summary>
/// entry-point extensions for wiring FastEndpoints-as-A2A into the ASP.NET Core host.
/// typical usage:
/// <code>
/// builder.Services
///     .AddFastEndpoints()
///     .AddA2A();
/// var app = builder.Build();
/// app.UseFastEndpoints()
///    .UseA2A();
/// </code>
/// </summary>
public static class Extensions
{
    /// <summary>
    /// registers the A2A services: skill dispatcher, agent-card builder, <see cref="EndpointInvoker" />.
    /// use <see cref="A2AOptions.SkillFilter" /> for startup/static skill inclusion and
    /// <see cref="A2AOptions.SkillVisibilityFilter" /> for per-request/per-caller skill visibility.
    /// </summary>
    public static IServiceCollection AddA2A(this IServiceCollection services, Action<A2AOptions>? configure = null)
    {
        var options = new A2AOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<EndpointInvoker>();
        services.AddSingleton<A2ASkillCatalog>();
        services.AddSingleton<AgentCardBuilder>();
        services.AddSingleton<A2ASkillDispatcher>();

        return services;
    }

    /// <summary>
    /// maps the A2A v1 agent card (<c>/.well-known/agent-card.json</c>) and JSON-RPC endpoint (default <c>/a2a</c>).
    /// <see cref="A2AOptions.SkillVisibilityFilter" /> is applied to both card generation and skill dispatch.
    /// call after <c>UseFastEndpoints()</c>. pass <paramref name="configureRpcRoute" /> /
    /// <paramref name="configureCardRoute" /> to chain conventions like <c>RequireAuthorization</c>.
    /// </summary>
    public static IApplicationBuilder UseA2A(this IApplicationBuilder app,
                                             string rpcPattern = "/a2a",
                                             string agentCardPattern = "/.well-known/agent-card.json",
                                             Action<IEndpointConventionBuilder>? configureRpcRoute = null,
                                             Action<IEndpointConventionBuilder>? configureCardRoute = null)
    {
        if (app is not IEndpointRouteBuilder routes)
        {
            throw new InvalidOperationException(
                "UseA2A must be called on an IApplicationBuilder that also implements IEndpointRouteBuilder (such as WebApplication). " +
                "Call UseA2A after building the WebApplication, or after UseRouting in a classic pipeline.");
        }

        routes.ServiceProvider.GetRequiredService<A2AOptions>().RpcPattern = rpcPattern;

        var cardRoute = routes.MapGet(
            agentCardPattern,
            (HttpContext ctx, AgentCardBuilder builder) =>
            {
                var card = builder.Build(ctx);

                return Results.Json(card, Config.SerOpts.Options);
            });
        configureCardRoute?.Invoke(cardRoute);

        var rpcRoute = routes.MapPost(
            rpcPattern,
            async (HttpContext ctx, A2ASkillDispatcher dispatcher) =>
            {
                var serializerOptions = Config.SerOpts.Options;
                JsonRpcRequest? req;

                try
                {
                    req = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(ctx.Request.Body, serializerOptions, ctx.RequestAborted);
                }
                catch (JsonException ex)
                {
                    return Results.Json(new JsonRpcResponse { Error = new() { Code = -32700, Message = $"parse error: {ex.Message}" } }, serializerOptions, statusCode: 400);
                }

                if (req is null || req.JsonRpc != "2.0" || string.IsNullOrEmpty(req.Method))
                    return Results.Json(new JsonRpcResponse { Error = new() { Code = -32600, Message = "invalid JSON-RPC request" } }, serializerOptions, statusCode: 400);

                var response = new JsonRpcResponse { Id = req.Id };

                try
                {
                    response.Result = await dispatcher.DispatchAsync(req.Method, req.Params, ctx, ctx.RequestAborted);
                }
                catch (A2ARpcException rpc)
                {
                    response.Result = null;
                    response.Error = rpc.Error;
                }
                catch (Exception ex)
                {
                    response.Result = null;
                    response.Error = JsonRpcError.Internal(ex.Message);
                }

                return Results.Json(response, serializerOptions);
            });
        configureRpcRoute?.Invoke(rpcRoute);

        return app;
    }
}
