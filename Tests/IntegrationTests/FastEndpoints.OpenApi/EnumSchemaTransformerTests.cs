using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OpenApi;

public class EnumSchemaTransformerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task property_converters_match_wire_values_without_changing_shared_enum_schemas(bool globalStrings)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

        if (globalStrings)
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        var sharedCtx = new SharedContext { SerializerOptions = options };
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.TypeInfoResolver = options.TypeInfoResolver;

            foreach (var converter in options.Converters)
                o.SerializerOptions.Converters.Add(converter);
        });
        var documentName = "v1";
        builder.Services.AddOpenApi(documentName, o =>
        {
            o.CreateSchemaReferenceId = SchemaNameGenerator.Create(true, sharedCtx.SchemaNames);
            o.AddSchemaTransformer(new EnumSchemaTransformer(sharedCtx));
        });

        await using var app = builder.Build();
        app.MapGet("/paint", () => new PaintResponse());
        app.MapPost("/paint", (PaintResponse response) => response);
        app.MapOpenApi();
        await app.StartAsync();
        using var client = app.GetTestClient();
        var wire = JsonNode.Parse(await client.GetStringAsync("/paint"))!;

        wire["colour"]!.GetValue<string>().ShouldBe("DarkBlue");
        wire["genericColour"]!.GetValue<string>().ShouldBe("DarkBlue");
        wire["nullableColour"]!.GetValue<string>().ShouldBe("DarkBlue");
        wire["camelColour"]!.GetValue<string>().ShouldBe("darkBlue");
        wire["integerColour"]!.GetValue<int>().ShouldBe(11);
        wire["typeColour"]!.GetValue<string>().ShouldBe(globalStrings ? "darkBlue" : "DarkBlue");

        for (var i = 0; i < 2; i++)
        {
            var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
            var schemas = document["components"]!["schemas"]!;
            var properties = schemas[SchemaNameGenerator.GetReferenceId(typeof(PaintResponse), true)!]!["properties"]!;

            AssertEnum(properties["colour"]!, "string", "Red", "DarkBlue");
            AssertEnum(properties["genericColour"]!, "string", "Red", "DarkBlue");
            AssertEnum(properties["camelColour"]!, "string", "red", "darkBlue");
            AssertEnum(properties["integerColour"]!, "integer", 10, 11);
            var nullable = properties["nullableColour"]!;
            nullable["$ref"].ShouldBeNull();
            nullable["type"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["null", "string"], ignoreOrder: true);
            nullable["enum"]!.AsArray().Select(n => n?.ToJsonString()).ShouldBe(["\"Red\"", "\"DarkBlue\"", null]);

            var colourId = SchemaNameGenerator.GetReferenceId(typeof(Colour), true)!;
            properties["ordinaryColour"]!["$ref"]!.GetValue<string>().ShouldBe($"#/components/schemas/{colourId}");

            if (globalStrings)
                AssertEnum(schemas[colourId]!, "string", "red", "darkBlue");
            else
                AssertEnum(schemas[colourId]!, "integer", 0, 1);

            AssertEnum(schemas[SchemaNameGenerator.GetReferenceId(typeof(StringColour), true)!]!, "string",
                       globalStrings ? "red" : "Red", globalStrings ? "darkBlue" : "DarkBlue");
        }

        using var body = new StringContent(wire.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/paint", body);
        response.EnsureSuccessStatusCode();
        JsonNode.DeepEquals(wire, JsonNode.Parse(await response.Content.ReadAsStringAsync())).ShouldBeTrue();

        wire["nullableColour"] = null;
        using var nullBody = new StringContent(wire.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        using var nullResponse = await client.PostAsync("/paint", nullBody);
        nullResponse.EnsureSuccessStatusCode();
        JsonNode.DeepEquals(wire, JsonNode.Parse(await nullResponse.Content.ReadAsStringAsync())).ShouldBeTrue();
    }

    static void AssertEnum(JsonNode schema, string type, params object[] values)
    {
        schema["$ref"].ShouldBeNull();
        schema["type"]!.GetValue<string>().ShouldBe(type);
        schema["enum"]!.AsArray().Select(n => n!.ToJsonString()).ShouldBe(values.Select(v => JsonSerializer.Serialize(v)));
    }

    public enum Colour { Red, DarkBlue }

    [JsonConverter(typeof(JsonStringEnumConverter<StringColour>))]
    public enum StringColour { Red, DarkBlue }

    public sealed class PaintResponse
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public Colour Colour { get; set; } = Colour.DarkBlue;

        public Colour OrdinaryColour { get; set; } = Colour.DarkBlue;

        [JsonConverter(typeof(JsonStringEnumConverter<Colour>))]
        public Colour GenericColour { get; set; } = Colour.DarkBlue;

        [JsonConverter(typeof(JsonStringEnumConverter<Colour>))]
        public Colour? NullableColour { get; set; } = Colour.DarkBlue;

        [JsonConverter(typeof(CamelColourConverter))]
        public Colour CamelColour { get; set; } = Colour.DarkBlue;

        [JsonConverter(typeof(IntegerColourConverter))]
        public Colour IntegerColour { get; set; } = Colour.DarkBlue;

        public StringColour TypeColour { get; set; } = StringColour.DarkBlue;
    }

    public sealed class CamelColourConverter() : JsonStringEnumConverter<Colour>(JsonNamingPolicy.CamelCase);

    public sealed class IntegerColourConverter : JsonConverter<Colour>
    {
        public override Colour Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => (Colour)(reader.GetInt32() - 10);

        public override void Write(Utf8JsonWriter writer, Colour value, JsonSerializerOptions options)
            => writer.WriteNumberValue((int)value + 10);
    }
}
