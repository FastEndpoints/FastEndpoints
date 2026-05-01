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
    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonRpcError? Error { get; set; }
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

/// <summary>A2A v1 <c>SendMessage</c> params.</summary>
sealed class A2AMessageSendParams
{
    [JsonPropertyName("tenant")] public string? Tenant { get; set; }
    [JsonPropertyName("message")] public A2AMessage? Message { get; set; }
    [JsonPropertyName("configuration")] public A2ASendMessageConfiguration? Configuration { get; set; }
    [JsonPropertyName("metadata")] public JsonElement? Metadata { get; set; }
}

sealed class A2ASendMessageConfiguration
{
    [JsonPropertyName("acceptedOutputModes")] public string[]? AcceptedOutputModes { get; set; }
    [JsonPropertyName("historyLength")] public int? HistoryLength { get; set; }
    [JsonPropertyName("returnImmediately")] public bool ReturnImmediately { get; set; }
}

sealed class A2ASendMessageResponse
{
    [JsonPropertyName("message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public A2AMessage? Message { get; init; }

    [JsonPropertyName("task")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Task { get; init; }
}

sealed class A2AMessage
{
    [JsonPropertyName("messageId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MessageId { get; set; }

    [JsonPropertyName("contextId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ContextId { get; set; }

    [JsonPropertyName("taskId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TaskId { get; set; }

    [JsonPropertyName("role")] public string Role { get; set; } = "ROLE_USER";
    [JsonPropertyName("parts")] public A2APart[]? Parts { get; set; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; set; }
}

sealed class A2APart
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("raw")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Raw { get; set; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; set; }

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Data { get; set; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Metadata { get; set; }

    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }

    [JsonPropertyName("mediaType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? MediaType { get; set; }
}
