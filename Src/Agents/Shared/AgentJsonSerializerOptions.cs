using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace FastEndpoints.Agents;

static class AgentJsonSerializerOptions
{
    internal static JsonSerializerOptions EnsureTypeInfoResolver(JsonSerializerOptions options)
        => options.TypeInfoResolver is not null
               ? options
               : new(options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
}