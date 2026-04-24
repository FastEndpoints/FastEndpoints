using System.Text.Json;
using FastEndpoints.Agents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.A2A;

/// <summary>
/// entry-point extensions for wiring FastEndpoints-as-A2A into the ASP.NET Core host.
/// </summary>
public static class Extensions
{
    /// <summary>registers the A2A services: skill dispatcher, agent-card builder, <see cref="EndpointInvoker" />.</summary>
    public static IServiceCollection AddFastEndpointsA2A(this IServiceCollection services, Action<A2AOptions>? configure = null)
    {
        var options = new A2AOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddSingleton<EndpointInvoker>();
        services.AddSingleton<AgentCardBuilder>();
        services.AddSingleton<A2ASkillDispatcher>();
        return services;
    }

    /// <summary>
    /// maps the A2A agent card (<c>/.well-known/agent.json</c>) and JSON-RPC endpoint (default <c>/a2a</c>).
    /// call after <c>UseFastEndpoints()</c>.
    /// </summary>
    public static void MapFastEndpointsA2A(this IEndpointRouteBuilder app, string rpcPattern = "/a2a", string agentCardPattern = "/.well-known/agent.json")
    {
        app.MapGet(agentCardPattern, (HttpContext ctx, AgentCardBuilder builder) =>
        {
            var card = builder.Build(ctx);
            return Results.Json(card, FastEndpoints.Config.SerOpts.Options);
        });

        app.MapPost(rpcPattern, async (HttpContext ctx, A2ASkillDispatcher dispatcher) =>
        {
            var serializerOptions = FastEndpoints.Config.SerOpts.Options;
            JsonRpcRequest? req;
            try
            {
                req = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(ctx.Request.Body, serializerOptions, ctx.RequestAborted);
            }
            catch (JsonException ex)
            {
                return Results.Json(new JsonRpcResponse { Error = new JsonRpcError { Code = -32700, Message = $"parse error: {ex.Message}" } }, serializerOptions, statusCode: 400);
            }

            if (req is null || req.JsonRpc != "2.0" || string.IsNullOrEmpty(req.Method))
                return Results.Json(new JsonRpcResponse { Error = new JsonRpcError { Code = -32600, Message = "invalid JSON-RPC request" } }, serializerOptions, statusCode: 400);

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
    }
}
