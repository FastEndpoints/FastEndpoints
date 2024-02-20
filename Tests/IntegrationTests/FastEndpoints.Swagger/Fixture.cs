using NSwag.Generation;

namespace Swagger;

public class Fixture : AppFixture<Web.Program>
{
    public Fixture(IMessageSink s) : base(s) { }

    public IOpenApiDocumentGenerator DocGenerator { get; set; } = default!;

    protected override Task SetupAsync()
    {
        DocGenerator = Services.GetRequiredService<IOpenApiDocumentGenerator>();

        return Task.CompletedTask;
    }
}