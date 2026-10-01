using FastEndpoints.OpenApi;
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

    [Fact]
    public void cloning_a_schema_does_not_add_a_const_member()
    {
        var source = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Items = new OpenApiSchema { Type = JsonSchemaType.String }
        };

        var clone = ((IOpenApiSchema)source).CloneAsConcreteSchema();

        clone.ShouldNotBeNull();
        Serialize(clone).ShouldNotContain("const");
    }

    [Fact]
    public void cloning_a_nullable_enum_preserves_null_values()
    {
        var value = JsonValue.Create("DarkBlue")!;
        var source = new OpenApiSchema
        {
            Type = JsonSchemaType.String | JsonSchemaType.Null,
            Enum = [value, null!]
        };

        var clone = ((IOpenApiSchema)source).CloneAsConcreteSchema()!;

        clone.Enum!.Count.ShouldBe(2);
        clone.Enum[0].ToJsonString().ShouldBe("\"DarkBlue\"");
        clone.Enum[0].ShouldNotBeSameAs(value);
        clone.Enum[1].ShouldBeNull();
    }

    static string Serialize(OpenApiSchema schema)
    {
        var writer = new StringWriter();
        schema.SerializeAsV31(new OpenApiJsonWriter(writer));

        return writer.ToString();
    }
}
