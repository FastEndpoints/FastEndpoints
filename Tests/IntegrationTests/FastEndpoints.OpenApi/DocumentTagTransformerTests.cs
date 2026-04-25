using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi;

public class DocumentTagTransformerTests
{
    [Fact]
    public void cleanup_preserves_existing_document_tags_when_tag_descriptions_are_not_configured()
    {
        var document = new OpenApiDocument
        {
            Tags = new HashSet<OpenApiTag>
            {
                new() { Name = "Generated" },
                new() { Name = "Custom", Description = "configured elsewhere" }
            }
        };

        DocumentTagTransformer.Cleanup(document, new());

        document.Tags.ShouldNotBeNull();
        var tag = document.Tags.Single();
        tag.Name.ShouldBe("Custom");
        tag.Description.ShouldBe("configured elsewhere");
    }
}
