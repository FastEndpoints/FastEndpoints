using System.Text;
using System.Text.Json;
using static FastEndpoints.Config;

namespace FastEndpoints;

static class X402Serializer
{
    internal static readonly JsonSerializerOptions Options = new(SerOpts.Options)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    internal static string ToBase64<T>(T value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Options)));

    internal static T FromBase64<T>(string value)
        => JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(Convert.FromBase64String(value)), Options)! ??
           throw new InvalidOperationException($"failed to deserialize x402 payload as [{typeof(T).Name}]!");
}