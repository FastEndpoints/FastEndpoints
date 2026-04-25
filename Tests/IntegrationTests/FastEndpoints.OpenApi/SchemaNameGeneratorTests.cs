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

    [Fact]
    public void simple_generic_schema_names_do_not_duplicate_argument_names()
    {
        var refId = SchemaNameGenerator.GetReferenceId(typeof(GenericType<SchemaNameCustomer>), shortSchemaNames: true);

        refId.ShouldBe("GenericTypeOfSchemaNameCustomer");
    }

    [Fact]
    public void nested_generic_schema_names_include_argument_names_once()
    {
        var refId = SchemaNameGenerator.GetReferenceId(
            typeof(GenericPair<string, GenericType<List<SchemaNameCustomer>>>),
            shortSchemaNames: true);

        refId.ShouldBe("GenericPairOfStringAndGenericTypeOfListOfSchemaNameCustomer");
    }
}

public class NestedTypeContainer
{
    public class Child { }
}

public class SchemaNameCustomer { }

public class GenericType<T> { }

public class GenericPair<T1, T2> { }
