using System.Text.Json;
using System.Text.Json.Nodes;

namespace FastEndpoints.OpenApi;

static partial class OperationSchemaHelpers
{
    extension(object? value)
    {
        internal JsonNode? JsonNodeFromObject()
        {
            if (value is null)
                return null;

            try
            {
                return JsonSerializer.SerializeToNode(value, Cfg.SerOpts.Options);
            }
            catch
            {
                return null;
            }
        }

        internal JsonObject? JsonObjectFromObject(Type? valueType = null)
        {
            if (value is null)
                return null;

            try
            {
                return valueType is null
                           ? JsonSerializer.SerializeToNode(value, Cfg.SerOpts.Options) as JsonObject
                           : JsonSerializer.SerializeToNode(value, valueType, Cfg.SerOpts.Options) as JsonObject;
            }
            catch
            {
                return null;
            }
        }
    }
}
