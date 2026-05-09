using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.A2A;

static class A2AJsonRpcEndpoint
{
    const string SupportedVersion = "1.0";

    public static async Task<IResult> HandleAsync(HttpContext context, A2ASkillDispatcher dispatcher, ILoggerFactory loggerFactory)
    {
        var serializerOptions = Config.SerOpts.Options;
        JsonRpcRequest? request;
        bool hasId;

        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
            hasId = document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("id", out _);
            request = document.RootElement.Deserialize<JsonRpcRequest>(serializerOptions);

            if (request?.Id is { } id)
                request.Id = id.Clone();
            if (request?.Params is { } parameters)
                request.Params = parameters.Clone();
        }
        catch (JsonException ex)
        {
            return Results.Json(new JsonRpcResponse { Error = new() { Code = -32700, Message = $"parse error: {ex.Message}" } }, serializerOptions, statusCode: 400);
        }

        if (request is null || request.JsonRpc != "2.0" || string.IsNullOrEmpty(request.Method))
            return Results.Json(new JsonRpcResponse { Error = new() { Code = -32600, Message = "invalid JSON-RPC request" } }, serializerOptions, statusCode: 400);

        if (TryGetRequestedA2AVersion(context, out var requestedVersion) && requestedVersion != SupportedVersion)
            return Results.Json(new JsonRpcResponse { Id = request.Id, Error = JsonRpcError.VersionNotSupported(requestedVersion) }, serializerOptions);

        if (!hasId)
        {
            try
            {
                await dispatcher.DispatchAsync(request.Method, request.Params, context, context.RequestAborted);
            }
            catch
            {
                // JSON-RPC notifications do not receive success or error responses.
            }

            return Results.NoContent();
        }

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
            loggerFactory.CreateLogger(typeof(A2AJsonRpcEndpoint)).LogError(ex, "A2A JSON-RPC request failed.");
            response.Result = null;
            response.Error = JsonRpcError.Internal("Internal error");
        }

        return Results.Json(response, serializerOptions);
    }

    static bool TryGetRequestedA2AVersion(HttpContext context, out string version)
    {
        version = context.Request.Headers["A2A-Version"].FirstOrDefault() ?? context.Request.Query["A2A-Version"].FirstOrDefault() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(version);
    }
}
