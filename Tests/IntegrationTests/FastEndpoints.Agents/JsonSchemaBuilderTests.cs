using System.Text.Json;

namespace FastEndpoints.Agents.Tests;

public class JsonSchemaBuilderTests
{
    class SimpleDto
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    [Fact]
    public void Build_emits_object_schema_with_properties()
    {
        var schema = JsonSchemaBuilder.Build(typeof(SimpleDto), JsonSerializerOptions.Default);
        schema.ShouldNotBeNull();

        var json = schema.ToJsonString();
        json.ShouldContain("\"Name\"");
        json.ShouldContain("\"Age\"");
    }

    [Fact]
    public void Build_returns_clones_so_callers_can_mutate_safely()
    {
        var a = JsonSchemaBuilder.Build(typeof(SimpleDto), JsonSerializerOptions.Default);
        var b = JsonSchemaBuilder.Build(typeof(SimpleDto), JsonSerializerOptions.Default);

        ReferenceEquals(a, b).ShouldBeFalse();
    }
}
