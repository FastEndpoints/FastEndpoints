using FastEndpoints.Testing;
using NSwag.Generation;
using Xunit.Abstractions;

namespace Swagger;

public class Fixture : TestFixture<Web.Program>
{
    public Fixture(IMessageSink s) : base(s) { }

    public IOpenApiDocumentGenerator DocGenerator { get; set; } = default!;

    protected override Task SetupAsync()
    {
        DocGenerator = Services.GetRequiredService<IOpenApiDocumentGenerator>();
        return Task.CompletedTask;
    }
}