using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints.A2A;

static class A2AJsonRpcEndpoint
{
    public static async Task<IResult> HandleAsync(HttpContext context, A2ASkillDispatcher dispatcher)
    {
        var serializerOptions = Config.SerOpts.Options;
        JsonRpcRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<JsonRpcRequest>(context.Request.Body, serializerOptions, context.RequestAborted);
        }
        catch (JsonException ex)
        {
            return Results.Json(new JsonRpcResponse { Error = new() { Code = -32700, Message = $"parse error: {ex.Message}" } }, serializerOptions, statusCode: 400);
        }

        if (request is null || request.JsonRpc != "2.0" || string.IsNullOrEmpty(request.Method))
            return Results.Json(new JsonRpcResponse { Error = new() { Code = -32600, Message = "invalid JSON-RPC request" } }, serializerOptions, statusCode: 400);

        var response = new JsonRpcResponse { Id = request.Id };

        try
        {
            response.Result = await dispatcher.DispatchAsync(request.Method, request.Params, context, context.RequestAborted);
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
    }
}
