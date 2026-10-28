#if NET11_0_OR_GREATER
global using OpenApiMediaTypeValue = Microsoft.OpenApi.IOpenApiMediaType;
#else
global using OpenApiMediaTypeValue = Microsoft.OpenApi.OpenApiMediaType;
#endif
using System.Text.Json.Nodes;
using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi;

public class MediaTypeCompatibilityTests
{
    [Fact]
    public void mutable_media_types_preserve_concrete_instances()
    {
        var mediaType = new OpenApiMediaType { Schema = new OpenApiSchema { Type = JsonSchemaType.String } };
        var content = new Dictionary<string, OpenApiMediaTypeValue> { ["application/json"] = mediaType };

        content.GetMutableMediaType("application/json").ShouldBeSameAs(mediaType);
        content.GetMutableMediaTypes().ShouldHaveSingleItem().ShouldBeSameAs(mediaType);
    }

    [Fact]
    public void schema_signatures_distinguish_xml_node_kinds()
    {
        var first = new OpenApiSchema { Xml = new() };
        var second = new OpenApiSchema { Xml = new() };
#if NET11_0_OR_GREATER
        first.Xml.NodeType = OpenApiXmlNodeType.Element;
        second.Xml.NodeType = OpenApiXmlNodeType.Attribute;
#else
        first.Xml.Attribute = false;
        second.Xml.Attribute = true;
#endif
        var firstSignature = SchemaSignatureBuilder.GetSchemaSignature("First", first, [], [], 0);
        var secondSignature = SchemaSignatureBuilder.GetSchemaSignature("Second", second, [], [], 0);

        firstSignature.ShouldNotBe(secondSignature);
    }

#if NET11_0_OR_GREATER
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void item_schema_references_are_collected_and_rewritten(bool inlineEnvelope)
    {
        var mediaType = new OpenApiMediaType
        {
            Schema = new OpenApiSchemaReference("Other"),
            ItemSchema = inlineEnvelope
                             ? new OpenApiSchema
                             {
                                 Properties = new Dictionary<string, IOpenApiSchema> { ["data"] = new OpenApiSchemaReference("Original") }
                             }
                             : new OpenApiSchemaReference("Original")
        };
        var document = new OpenApiDocument
        {
            Components = new()
            {
                Responses = new Dictionary<string, IOpenApiResponse>
                {
                    ["Sse"] = new OpenApiResponse
                    {
                        Content = new Dictionary<string, IOpenApiMediaType> { ["text/event-stream"] = mediaType }
                    }
                }
            }
        };

        document.GetReferencedSchemaRefs().ShouldContain("Original");
        OpenApiSchemaGraphTransformer.TransformDocumentSchemas(
            document,
            schema => schema is OpenApiSchemaReference { Reference.Id: "Original" }
                          ? new OpenApiSchemaReference("Rewritten")
                          : schema);

        ((OpenApiSchemaReference)mediaType.Schema).Reference.Id.ShouldBe("Other");
        var item = inlineEnvelope ? mediaType.ItemSchema!.Properties!["data"] : mediaType.ItemSchema;
        ((OpenApiSchemaReference)item!).Reference.Id.ShouldBe("Rewritten");
        document.GetReferencedSchemaRefs().ShouldNotContain("Original");
        document.GetReferencedSchemaRefs().ShouldContain("Rewritten");
    }

    [Fact]
    public void referenced_media_types_are_localized_without_mutating_the_component()
    {
        var component = new OpenApiMediaType
        {
            Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            Example = JsonValue.Create("original"),
            Examples = new Dictionary<string, IOpenApiExample>
            {
                ["named"] = new OpenApiExample { Value = JsonValue.Create("named") }
            }
        };
        var document = new OpenApiDocument
        {
            Components = new() { MediaTypes = new Dictionary<string, IOpenApiMediaType> { ["Shared"] = component } }
        };
        document.RegisterComponents();
        var firstReference = new OpenApiMediaTypeReference("Shared", document);
        var secondReference = new OpenApiMediaTypeReference("Shared", document);
        var content = new Dictionary<string, OpenApiMediaTypeValue>
        {
            ["application/json"] = firstReference,
            ["text/plain"] = secondReference
        };

        var localized = content.GetMutableMediaTypes().ToArray();
        localized.Length.ShouldBe(2);
        localized[0].ShouldNotBeSameAs(component);
        localized[0].ShouldNotBeSameAs(localized[1]);
        content["application/json"].ShouldBeSameAs(localized[0]);
        localized[0].Example = JsonValue.Create("changed");
        localized[0].Examples!.Clear();
        localized[0].Schema = new OpenApiSchema { Type = JsonSchemaType.Integer };

        component.Example!.GetValue<string>().ShouldBe("original");
        component.Examples!.ShouldHaveSingleItem();
        component.Schema!.Type.ShouldBe(JsonSchemaType.String);
        localized[1].Example!.GetValue<string>().ShouldBe("original");
    }

    [Fact]
    public void unresolved_media_type_references_fail_without_replacing_the_reference()
    {
        var reference = new OpenApiMediaTypeReference("Missing");
        var content = new Dictionary<string, OpenApiMediaTypeValue> { ["application/json"] = reference };

        Should.Throw<InvalidOperationException>(() => content.GetMutableMediaType("application/json"));
        content["application/json"].ShouldBeSameAs(reference);
    }
#endif
}
