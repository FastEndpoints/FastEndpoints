using NSwag.Generation;

namespace Swagger;

public class Fixture : AppFixture<Web.Program>
{
    public IOpenApiDocumentGenerator DocGenerator { get; set; } = default!;

    protected override ValueTask SetupAsync()
    {
        DocGenerator = Services.GetRequiredService<IOpenApiDocumentGenerator>();

        return ValueTask.CompletedTask;
    }
}