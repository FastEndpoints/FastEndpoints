using System.Text.Json;
using System.Text.Json.Serialization;

namespace FastEndpoints.A2A;

/// <summary>minimal JSON-RPC 2.0 request envelope used by A2A.</summary>
sealed class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public JsonElement? Id { get; set; }
    [JsonPropertyName("method")] public string Method { get; set; } = "";
    [JsonPropertyName("params")] public JsonElement? Params { get; set; }
}

sealed class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")] public string JsonRpc { get; set; } = "2.0";
    [JsonPropertyName("id")] public JsonElement? Id { get; set; }
    [JsonPropertyName("result")] public object? Result { get; set; }
    [JsonPropertyName("error")] public JsonRpcError? Error { get; set; }
}

sealed class JsonRpcError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("data")] public object? Data { get; set; }

    public static JsonRpcError MethodNotFound(string method) => new() { Code = -32601, Message = $"Method not found: {method}" };
    public static JsonRpcError InvalidParams(string msg) => new() { Code = -32602, Message = msg };
    public static JsonRpcError Internal(string msg, object? data = null) => new() { Code = -32603, Message = msg, Data = data };
}

/// <summary>A2A <c>message/send</c> params: a single skill call with its input message.</summary>
sealed class A2AMessageSendParams
{
    [JsonPropertyName("skill")] public string? Skill { get; set; }
    [JsonPropertyName("message")] public A2AMessage? Message { get; set; }
}

sealed class A2AMessage
{
    [JsonPropertyName("role")] public string Role { get; set; } = "user";
    [JsonPropertyName("parts")] public A2APart[]? Parts { get; set; }
}

sealed class A2APart
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = "data";
    [JsonPropertyName("text")] public string? Text { get; set; }
    [JsonPropertyName("data")] public JsonElement? Data { get; set; }
    [JsonPropertyName("mimeType")] public string? MimeType { get; set; }
}
