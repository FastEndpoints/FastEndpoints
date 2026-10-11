// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

static class ValidationSchemaMutation
{
    internal static OpenApiSchema? ResolveForMutation(IOpenApiSchema? schema,
                                                      bool localizeReferencedSchemas,
                                                      SharedContext sharedCtx,
                                                      OpenApiGenerationState generation,
                                                      string operationKey,
                                                      string schemaKey,
                                                      Action<IOpenApiSchema> replace)
    {
        if (!localizeReferencedSchemas || schema is not OpenApiSchemaReference)
            return schema.ResolveSchema();

        return schema.EnsureSchemaForMutation(sharedCtx, generation, operationKey, schemaKey, replace);
    }

    internal static OpenApiSchema? ResolveArrayItemsForMutation(OpenApiSchema? arraySchema,
                                                                bool localizeReferencedSchemas,
                                                                SharedContext sharedCtx,
                                                                OpenApiGenerationState generation,
                                                                string operationKey,
                                                                string schemaKey,
                                                                string propertyPath)
    {
        if (arraySchema?.Type?.HasFlag(JsonSchemaType.Array) != true)
            return null;

        return ResolveForMutation(
            arraySchema.Items,
            localizeReferencedSchemas,
            sharedCtx,
            generation,
            operationKey,
            $"{schemaKey}.{propertyPath}[]",
            localized => arraySchema.Items = localized);
    }

    internal static OpenApiSchema? ResolveItemsSchema(OpenApiSchema? propertySchema,
                                                      bool localizeReferencedSchemas,
                                                      SharedContext sharedCtx,
                                                      OpenApiGenerationState generation,
                                                      string operationKey,
                                                      string schemaKey,
                                                      string fullPropertyName)
    {
        if (propertySchema?.Type?.HasFlag(JsonSchemaType.Array) != true)
        {
            if (propertySchema?.OneOf is not { Count: 2 } options)
                return null;

            var arrayIndex = options[0] is OpenApiSchema { Type: JsonSchemaType.Null }
                                 ? 1
                                 : options[1] is OpenApiSchema { Type: JsonSchemaType.Null } ? 0 : -1;

            if (arrayIndex < 0 || options[arrayIndex].ResolveSchema()?.Type?.HasFlag(JsonSchemaType.Array) != true)
                return null;

            var arrayOption = options[arrayIndex];
            propertySchema = localizeReferencedSchemas && arrayOption is OpenApiSchemaReference
                                 ? arrayOption.CloneAsConcreteSchema()
                                 : arrayOption.ResolveSchema();

            if (propertySchema is not null && localizeReferencedSchemas)
            {
                // Nullable-array normalization needs a resolvable branch before variant references are rebound.
                options[arrayIndex] = propertySchema;

                if (arrayOption.Description is { Length: > 0 } description)
                    propertySchema.Description = description;
            }
        }

        return ResolveArrayItemsForMutation(
            propertySchema,
            localizeReferencedSchemas,
            sharedCtx,
            generation,
            operationKey,
            schemaKey,
            fullPropertyName);
    }
}
