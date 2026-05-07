using System.Text.Json;
using Microsoft.OpenApi;
namespace FastEndpoints.OpenApi;

sealed class RequestBodyOverrideApplicator(DocumentOptions docOpts, SharedContext sharedCtx)
{
    JsonNamingPolicy? NamingPolicy => sharedCtx.NamingPolicy;

    internal PromotedBodyProperty? Apply(OpenApiOperation operation, EndpointDefinition epDef, string operationKey)
    {
        if (operation.RequestBody?.Content is null)
            return null;

        var requestDtoType = epDef.ReqDtoType;

        var (promoteProp, fromBodyProp, fromFormProp) = PromotedBodyPropertyResolver.Find(requestDtoType);

        if (promoteProp is null)
            return null;

        var promoted = false;
        var mutationCtx = new OperationSchemaMutationContext(sharedCtx, operationKey);
        var schemaKey = PropertyNameResolver.GetSchemaPropertyName(promoteProp, NamingPolicy, docOpts.UsePropertyNamingPolicy);

        // replace the entire request body schema with the [FromBody]/[FromForm] property's type schema
        foreach (var content in operation.RequestBody.Content.Values)
        {
            var resolvedSchema = content.Schema.ResolveSchema(sharedCtx);

            if (resolvedSchema is null)
                continue;

            var matchingKey = resolvedSchema.Properties?.Keys.FindCaseInsensitiveKey(schemaKey);

            if (matchingKey is not null && resolvedSchema.Properties!.TryGetValue(matchingKey, out var propSchema))
            {
                content.Schema = propSchema;
                content.EnsureOperationLocalSchemaForMutation(mutationCtx, "requestBody");
                promoted = true;
            }
        }

        if (promoted && SchemaNameGenerator.GetReferenceId(requestDtoType, docOpts.ShortSchemaNames) is { } refId)
            sharedCtx.PromotedRequestWrapperSchemaRefs.TryAdd(refId, 0);

        if (promoted && fromFormProp is not null)
            NormalizePromotedFormRequestBodyContent(operation, epDef.FormDataContentType);

        // JSON Patch unwrap: only for [FromBody], promote the operations array to top-level
        if (fromBodyProp is not null && operation.RequestBody.Content.TryGetValue("application/json-patch+json", out var patchContent))
        {
            var patchArraySchema = TryGetJsonPatchArraySchema(patchContent.Schema);

            if (patchArraySchema is not null)
            {
                patchContent.Schema = patchArraySchema;
                patchContent.EnsureOperationLocalSchemaForMutation(mutationCtx, "requestBody.jsonPatch");
            }
        }

        return promoted ? new(schemaKey, promoteProp.PropertyType) : null;
    }

    static void NormalizePromotedFormRequestBodyContent(OpenApiOperation operation, string? formDataContentType)
    {
        if (operation.RequestBody?.Content is not { Count: > 0 } content)
            return;

        var targetContentType = string.IsNullOrWhiteSpace(formDataContentType)
                                    ? "multipart/form-data"
                                    : formDataContentType;
        var targetKey = content.Keys.FindCaseInsensitiveKey(targetContentType);
        var targetContent = targetKey is not null
                                ? content[targetKey]
                                : content.Values.First();

        content.Clear();
        content[targetContentType] = targetContent;
    }

    OpenApiSchema? TryGetJsonPatchArraySchema(IOpenApiSchema? schema)
    {
        var resolved = schema.ResolveSchema(sharedCtx);

        if (resolved is not { Type: JsonSchemaType.Object, Properties.Count: 1 })
            return null;

        var operationsProp = resolved.Properties
                                     .FirstOrDefault(p => string.Equals(p.Key, "operations", StringComparison.OrdinalIgnoreCase))
                                     .Value;

        return operationsProp.ResolveSchema(sharedCtx) is { Type: JsonSchemaType.Array } arraySchema
                   ? arraySchema
                   : null;
    }
}
