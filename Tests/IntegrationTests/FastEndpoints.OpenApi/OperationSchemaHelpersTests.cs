using System.Reflection;
using System.Text.Json.Nodes;
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
            Example = new JsonObject { ["name"] = "original" },
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["name"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["nested"] = new OpenApiSchema { Type = JsonSchemaType.String }
                    }
                }
            }
        };

        var original = (OpenApiSchema)schema;
        var clone = schema.CloneAsConcreteSchema()!;

        clone.ShouldNotBeNull();
        clone.ShouldNotBeSameAs(schema);
        clone.Required!.ShouldNotBeSameAs(original.Required);
        clone.Properties!.ShouldNotBeSameAs(original.Properties);
        clone.Example.ShouldNotBeSameAs(original.Example);

        clone.Required!.Add("other");
        ((JsonObject)clone.Example!)["name"] = "clone";
        ((OpenApiSchema)clone.Properties!["name"]).Properties!.Remove("nested");

        original.Required.ShouldBe(["name"]);
        original.Properties!.Keys.ShouldBe(["name"]);
        ((OpenApiSchema)original.Properties!["name"]).Properties!.Keys.ShouldBe(["nested"]);
        original.Example!["name"]!.GetValue<string>().ShouldBe("original");
    }

    [Fact]
    public void clone_logic_accounts_for_all_mutable_openapi_schema_members()
    {
        var unsupportedMutableMembers = typeof(OpenApiSchema).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                             .Where(p => p.CanRead && p.CanWrite)
                                                             .Where(p => !IsKnownCloneableOrShareable(p.PropertyType))
                                                             .Select(p => $"{p.Name}: {p.PropertyType.FullName}")
                                                             .ToArray();

        unsupportedMutableMembers.ShouldBeEmpty();

        static bool IsKnownCloneableOrShareable(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;

            if (type.IsValueType || type == typeof(string) || type == typeof(Type) || type == typeof(Uri))
                return true;

            if (typeof(JsonNode).IsAssignableFrom(type) ||
                typeof(IOpenApiSchema).IsAssignableFrom(type) ||
                type == typeof(OpenApiSchema) ||
                type == typeof(OpenApiSchemaReference) ||
                type == typeof(IDictionary<string, IOpenApiSchema>) ||
                type == typeof(IDictionary<string, string>) ||
                type == typeof(IDictionary<string, bool>) ||
                type == typeof(IDictionary<string, JsonNode>) ||
                type == typeof(IDictionary<string, HashSet<string>>) ||
                type == typeof(IDictionary<string, IOpenApiExtension>) ||
                type == typeof(IDictionary<string, object>) ||
                type == typeof(IList<IOpenApiSchema>) ||
                type == typeof(ISet<string>) ||
                type == typeof(IList<JsonNode>) ||
                type == typeof(OpenApiDiscriminator) ||
                type == typeof(OpenApiExternalDocs) ||
                type == typeof(OpenApiXml))
                return true;

            return false;
        }
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
