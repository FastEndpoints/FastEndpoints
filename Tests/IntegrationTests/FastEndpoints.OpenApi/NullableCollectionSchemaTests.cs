using Microsoft.OpenApi;

namespace OpenApi;

public class NullableCollectionSchemaTests
{
    [Fact]
    public void nullable_array_types_are_detected_as_arrays_via_flags()
    {
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Array | JsonSchemaType.Null,
            Items = new OpenApiSchema { Type = JsonSchemaType.Object }
        };

        (schema.Type.HasValue && schema.Type.Value.HasFlag(JsonSchemaType.Array) && schema.Items is not null).ShouldBeTrue();
    }
}
