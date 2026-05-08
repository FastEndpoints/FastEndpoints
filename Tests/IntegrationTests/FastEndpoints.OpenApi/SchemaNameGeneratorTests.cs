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

    [Fact]
    public void nullable_enum_schema_names_reuse_underlying_enum_schema_name()
    {
        var refId = SchemaNameGenerator.GetReferenceId(typeof(NullableSchemaNameStatus?), shortSchemaNames: true);

        refId.ShouldBe(SchemaNameGenerator.GetReferenceId(typeof(NullableSchemaNameStatus), shortSchemaNames: true));
        refId.ShouldBe("NullableSchemaNameStatus");
    }

    [Fact]
    public void array_schema_names_are_oas_component_key_safe()
    {
        var refId = SchemaNameGenerator.GetReferenceId(typeof(int[]), shortSchemaNames: false);

        refId.ShouldBe("SystemInt32Array");
    }

    [Fact]
    public void short_array_schema_names_are_oas_component_key_safe()
    {
        var refId = SchemaNameGenerator.GetReferenceId(typeof(SchemaNameCustomer[]), shortSchemaNames: true);

        refId.ShouldBe("SchemaNameCustomerArray");
    }

    [Fact]
    public void generic_schema_names_with_array_args_are_oas_component_key_safe()
    {
        var refId = SchemaNameGenerator.GetReferenceId(typeof(GenericType<int[]>), shortSchemaNames: false);

        refId.ShouldBe("OpenApiGenericTypeOfSystemInt32Array");
    }
}

public class NestedTypeContainer
{
    public class Child { }
}

public class SchemaNameCustomer { }

public class GenericType<T> { }

public class GenericPair<T1, T2> { }

public enum NullableSchemaNameStatus
{
    Active,
    Disabled
}

public static class ApiA
{
    public class User { }

    public class Page<T> { }
}

public static class ApiB
{
    public class User { }
}
