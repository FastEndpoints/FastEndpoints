using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi;

public class OperationSchemaHelpersTests
{
    [Fact]
    public void clone_as_concrete_schema_deep_copies_mutable_members()
    {
        IOpenApiSchema schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "name" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema { Type = JsonSchemaType.String }
            }
        };

        var original = (OpenApiSchema)schema;
        var clone = schema.CloneAsConcreteSchema()!;

        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(schema);
        clone.Required!.ShouldNotBeSameAs(original.Required);
        clone.Properties!.ShouldNotBeSameAs(original.Properties);

        clone.Required!.Add("other");
        clone.Properties!.Remove("name");

        original.Required.ShouldBe(["name"]);
        original.Properties!.Keys.ShouldBe(["name"]);
    }

    [Fact]
    public void json_helpers_return_null_when_serialization_fails()
    {
        var value = new ThrowingSerializableObject();

        value.JsonNodeFromObject().ShouldBeNull();
        value.JsonObjectFromObject().ShouldBeNull();
    }

    sealed class ThrowingSerializableObject
    {
        public string Value => throw new InvalidOperationException("serialization failed");
    }
}
