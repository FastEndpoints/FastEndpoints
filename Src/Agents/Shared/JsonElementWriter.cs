using System.Buffers;
using System.Text.Json;

namespace FastEndpoints.Agents;

static class JsonElementWriter
{
    // JsonSerializerOptions/JsonDocument default; Utf8JsonWriter alone would default to 1000
    const int DefaultMaxDepth = 64;

    /// <summary>
    /// writes a json object via the supplied callback and returns it as a standalone <see cref="JsonElement" />.
    /// a <paramref name="maxDepth" /> of 0 uses the serializer default of 64 for both writing and re-reading.
    /// </summary>
    internal static JsonElement WriteObject<TState>(TState state,
                                                    Action<Utf8JsonWriter, TState> writeProperties,
                                                    int maxDepth = 0)
    {
        if (maxDepth <= 0)
            maxDepth = DefaultMaxDepth;

        var reader = new Utf8JsonReader(WriteObjectUtf8(state, writeProperties, maxDepth).Span, new JsonReaderOptions { MaxDepth = maxDepth });

        return JsonElement.ParseValue(ref reader);
    }

    /// <summary>
    /// writes a json object via the supplied callback and returns its utf-8 bytes without re-parsing them.
    /// a <paramref name="maxDepth" /> of 0 uses the serializer default of 64.
    /// </summary>
    internal static ReadOnlyMemory<byte> WriteObjectUtf8<TState>(TState state,
                                                                 Action<Utf8JsonWriter, TState> writeProperties,
                                                                 int maxDepth = 0)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, new() { MaxDepth = maxDepth <= 0 ? DefaultMaxDepth : maxDepth }))
        {
            writer.WriteStartObject();
            writeProperties(writer, state);
            writer.WriteEndObject();
        }

        return buffer.WrittenMemory;
    }

    /// <summary>
    /// writes a json element with default writer options and returns its utf-8 bytes.
    /// </summary>
    internal static ReadOnlyMemory<byte> WriteUtf8(JsonElement element)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
            element.WriteTo(writer);

        return buffer.WrittenMemory;
    }
}
