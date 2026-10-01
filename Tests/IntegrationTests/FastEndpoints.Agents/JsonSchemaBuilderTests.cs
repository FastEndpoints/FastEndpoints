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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_emits_property_descriptions_with_serializer_metadata(bool sourceGenerated)
    {
        var options = sourceGenerated
                          ? DescriptionSchemaContext.Default.Options
                          : new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var schema = JsonSchemaBuilder.Build(typeof(DescribedSchemaDto), options);
        var properties = schema["properties"]!;

        properties["location"]!["description"]!.GetValue<string>().ShouldBe("City name or zip code");
        properties["display_name"]!["description"]!.GetValue<string>().ShouldBe("Display name");
        properties["nested"]!["properties"]!["location"]!["description"]!.GetValue<string>().ShouldBe("Nested location");
        properties["unannotated"]!["description"].ShouldBeNull();
        properties["emptyDescription"]!["description"].ShouldBeNull();
        properties["anything"]!["description"]!.GetValue<string>().ShouldBe("Any JSON value");
        properties["ignored"].ShouldBeNull();

        properties["location"]!["description"] = "mutated";
        JsonSchemaBuilder.Build(typeof(DescribedSchemaDto), options)["properties"]!["location"]!["description"]!
                         .GetValue<string>().ShouldBe("City name or zip code");
    }
}

public class DescribedSchemaDto
{
    [System.ComponentModel.Description("City name or zip code")]
    public string? Location { get; set; }

    [System.ComponentModel.Description("Display name")]
    [System.Text.Json.Serialization.JsonPropertyName("display_name")]
    public string? Name { get; set; }

    public NestedDescribedSchemaDto Nested { get; set; } = new();
    public int Unannotated { get; set; }

    [System.ComponentModel.Description("")]
    public string? EmptyDescription { get; set; }

    [System.ComponentModel.Description("Any JSON value")]
    public object? Anything { get; set; }

    [System.ComponentModel.Description("Hidden")]
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Ignored { get; set; }
}

public class NestedDescribedSchemaDto
{
    [System.ComponentModel.Description("Nested location")]
    public string? Location { get; set; }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(DescribedSchemaDto))]
internal partial class DescriptionSchemaContext : System.Text.Json.Serialization.JsonSerializerContext;
