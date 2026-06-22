using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using static FastEndpoints.Config;

namespace FastEndpoints;

static class X402Serializer
{
    internal static readonly JsonSerializerOptions Options = new(SerOpts.Options)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static readonly X402JsonContext Context = new(Options);

    internal static string ToBase64<T>(T value, JsonTypeInfo<T> typeInfo)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, typeInfo)));

    internal static T FromBase64<T>(string value, JsonTypeInfo<T> typeInfo)
        => JsonSerializer.Deserialize(Encoding.UTF8.GetString(Convert.FromBase64String(value)), typeInfo)! ??
           throw new InvalidOperationException($"failed to deserialize x402 payload as [{typeof(T).Name}]!");
}

[JsonSerializable(typeof(PaymentPayload)), JsonSerializable(typeof(PaymentRequiredResponse)), JsonSerializable(typeof(VerificationRequest)),
 JsonSerializable(typeof(VerificationResponse)), JsonSerializable(typeof(SettlementRequest)), JsonSerializable(typeof(SettlementResponse))]
sealed partial class X402JsonContext : JsonSerializerContext;