using NSwag.Generation;

namespace Swagger;

public class Fixture(IMessageSink s) : AppFixture<Web.Program>(s)
{
    public IOpenApiDocumentGenerator DocGenerator { get; set; } = default!;

    protected override Task SetupAsync()
    {
        DocGenerator = Services.GetRequiredService<IOpenApiDocumentGenerator>();

        return Task.CompletedTask;
    }
}