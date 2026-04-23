using FastEndpoints.OpenApi;

namespace OpenApi;

public class SchemaNameGeneratorTests
{
    [Fact]
    public void short_schema_names_replace_nested_type_separators()
    {
        var refId = SchemaNameGenerator.GetReferenceId(typeof(NestedTypeContainer.Child), shortSchemaNames: true);

        refId.ShouldBe("NestedTypeContainer_Child");
    }
}

public class NestedTypeContainer
{
    public class Child { }
}
