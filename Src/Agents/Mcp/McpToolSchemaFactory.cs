using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FastEndpoints.Agents;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Mcp;

static class McpToolSchemaFactory
{
    public static JsonNode BuildInputSchema(EndpointDefinition def, JsonSerializerOptions serializerOptions, string toolName, IServiceProvider services)
    {
        var inputSchema = BuildInputSchema(def, serializerOptions, toolName);

        RemoveNonClientInputProperties(inputSchema, def, serializerOptions);
        EnrichInputSchemaWithValidation(inputSchema, def, serializerOptions, services);
        DisallowAdditionalInputProperties(inputSchema);

        return inputSchema;
    }

    public static JsonNode? TryBuildOutputSchema(EndpointDefinition def, JsonSerializerOptions serializerOptions, McpOptions options)
    {
        if (!options.IncludeOutputSchemas || def.ResDtoType == typeof(object) || def.ResDtoType == typeof(void))
            return null;

        var outputSchema = JsonSchemaBuilder.Build(def.ResDtoType, serializerOptions);
        NormalizeRootObjectSchema(outputSchema);

        if (HasObjectRootSchema(outputSchema))
            RemoveToHeaderOutputProperties(outputSchema, def, serializerOptions);

        return HasObjectRootSchema(outputSchema) ? outputSchema : null;
    }

    public static JsonSerializerOptions ResolveSerializerOptions(EndpointDefinition def, JsonSerializerOptions fallback)
        => def.SerializerContext?.Options is { } options
               ? AgentJsonSerializerOptions.EnsureTypeInfoResolver(options)
               : fallback;

    static JsonNode BuildInputSchema(EndpointDefinition def, JsonSerializerOptions serializerOptions, string toolName)
    {
        if (def.ReqDtoType == Types.EmptyRequest)
        {
            return new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["additionalProperties"] = false
            };
        }

        var inputSchema = JsonSchemaBuilder.Build(def.ReqDtoType, serializerOptions);
        NormalizeRootObjectSchema(inputSchema);
        EnsureObjectRootSchema(inputSchema, toolName, def.ReqDtoType, "input", "arguments");

        return inputSchema;
    }

    static void NormalizeRootObjectSchema(JsonNode schema)
    {
        if (schema is not JsonObject obj || obj["type"] is not JsonArray types)
            return;

        foreach (var type in types)
        {
            if (type?.GetValue<string>() == "object")
            {
                obj["type"] = "object";

                return;
            }
        }
    }

    static void EnsureObjectRootSchema(JsonNode schema, string toolName, Type dtoType, string schemaKind, string shapeDescription)
    {
        if (HasObjectRootSchema(schema))
            return;

        throw new InvalidOperationException(
            $"MCP tool '{toolName}' cannot use {schemaKind} schema generated from '{dtoType.FullName ?? dtoType.Name}' with root type '{GetRootSchemaType(schema)}' because MCP tools require an object root schema. Use an object-shaped DTO for tool {shapeDescription}.");
    }

    static bool HasObjectRootSchema(JsonNode schema)
    {
        if (schema is not JsonObject obj || obj["type"] is not { } typeNode)
            return false;

        return typeNode switch
        {
            JsonValue value => value.TryGetValue<string>(out var type) && type == "object",
            JsonArray types => types.Any(type => type is JsonValue value && value.TryGetValue<string>(out var entry) && entry == "object"),
            _ => false
        };
    }

    static string GetRootSchemaType(JsonNode schema)
    {
        if (schema is not JsonObject obj || obj["type"] is not { } typeNode)
            return "<unspecified>";

        return typeNode switch
        {
            JsonValue value when value.TryGetValue<string>(out var type) => type,
            JsonArray types => string.Join("|", types.Select(type => type?.ToJsonString() ?? "null")),
            _ => typeNode.ToJsonString()
        };
    }

    static void EnrichInputSchemaWithValidation(JsonNode inputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions, IServiceProvider services)
    {
        if (def.ValidatorType is null)
            return;

        using var scope = services.CreateScope();

        if (TryResolveValidator(scope.ServiceProvider, def.ValidatorType) is { } validator)
            FluentValidationSchemaEnricher.Enrich(inputSchema, validator, def.ReqDtoType, serializerOptions);
    }

    static void DisallowAdditionalInputProperties(JsonNode inputSchema)
    {
        if (inputSchema is JsonObject root && HasObjectRootSchema(root))
            root["additionalProperties"] = false;
    }

    static IValidator? TryResolveValidator(IServiceProvider services, Type validatorType)
        => services.GetService(validatorType) as IValidator ?? (IValidator?)ActivatorUtilities.CreateInstance(services, validatorType);

    static void RemoveNonClientInputProperties(JsonNode inputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions)
        => RemoveSchemaProperties(inputSchema, def.ReqDtoType, serializerOptions, AgentInputPropertyRules.ShouldIgnoreClientInput);

    static void RemoveToHeaderOutputProperties(JsonNode outputSchema, EndpointDefinition def, JsonSerializerOptions serializerOptions)
        => RemoveSchemaProperties(outputSchema, def.ResDtoType, serializerOptions, prop => prop.IsDefined(Types.ToHeaderAttribute, true));

    static void RemoveSchemaProperties(JsonNode schema, Type dtoType, JsonSerializerOptions serializerOptions, Func<PropertyInfo, bool> shouldRemove)
    {
        if (schema is not JsonObject root || root["properties"] is not JsonObject props)
            return;

        var required = root["required"] as JsonArray;

        foreach (var prop in dtoType.BindableProps())
        {
            if (!shouldRemove(prop))
                continue;

            foreach (var name in AgentJsonPropertyNames.GetSchemaNameCandidates(prop, dtoType, serializerOptions))
            {
                props.Remove(name);
                RemoveRequiredEntry(required, name);
            }
        }

        if (required is { Count: 0 })
            root.Remove("required");
    }

    static void RemoveRequiredEntry(JsonArray? required, string name)
    {
        if (required is null)
            return;

        for (var i = required.Count - 1; i >= 0; i--)
        {
            if (required[i]?.GetValue<string>() == name)
                required.RemoveAt(i);
        }
    }
}