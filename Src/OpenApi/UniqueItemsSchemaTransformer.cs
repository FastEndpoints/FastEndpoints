using System.Reflection;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class UniqueItemsSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken ct)
    {
        if (!schema.Type.HasValue || !schema.Type.Value.HasFlag(JsonSchemaType.Array))
            return Task.CompletedTask;

        var declaredType = context.JsonPropertyInfo?.PropertyType ?? context.JsonTypeInfo.Type;
        var member = context.JsonPropertyInfo?.AttributeProvider as MemberInfo;

        OperationSchemaHelpers.ApplyUniqueItems(schema, declaredType, member);

        return Task.CompletedTask;
    }
}
