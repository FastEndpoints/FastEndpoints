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

    [Fact]
    public void long_schema_names_include_generic_argument_namespaces()
    {
        var userFromApiA = SchemaNameGenerator.GetReferenceId(typeof(ApiA.Page<ApiA.User>), shortSchemaNames: false);
        var userFromApiB = SchemaNameGenerator.GetReferenceId(typeof(ApiA.Page<ApiB.User>), shortSchemaNames: false);

        userFromApiA.ShouldBe("OpenApiApiA_PageOfOpenApiApiA_User");
        userFromApiB.ShouldBe("OpenApiApiA_PageOfOpenApiApiB_User");
        userFromApiA.ShouldNotBe(userFromApiB);
    }

    [Fact]
    public void short_schema_names_keep_short_generic_argument_names()
    {
        var userFromApiA = SchemaNameGenerator.GetReferenceId(typeof(ApiA.Page<ApiA.User>), shortSchemaNames: true);
        var userFromApiB = SchemaNameGenerator.GetReferenceId(typeof(ApiA.Page<ApiB.User>), shortSchemaNames: true);

        userFromApiA.ShouldBe("ApiA_PageOfUser");
        userFromApiB.ShouldBe("ApiA_PageOfUser");
    }
}

public class NestedTypeContainer
{
    public class Child { }
}

public class SchemaNameCustomer { }

public class GenericType<T> { }

public class GenericPair<T1, T2> { }

public static class ApiA
{
    public class User { }

    public class Page<T> { }
}

public static class ApiB
{
    public class User { }
}
