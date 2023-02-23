using FluentAssertions.Json;
using IntegrationTests.Shared.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NSwag.Generation;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.Swagger.IntegrationTests;

public class SwaggerDocTests : EndToEndTestBase
{
    private readonly IServiceProvider serviceProvider;

    public SwaggerDocTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
        serviceProvider = endToEndTestFixture.ServiceProvider;
    }

    [Fact]
    public async Task initial_release_doc_produces_correct_output()
    {
        var generator = serviceProvider.GetRequiredService<IOpenApiDocumentGenerator>();
        var doc = await generator.GenerateAsync("Initial Release");

        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);
        //await File.WriteAllTextAsync("initial-release.json", json);

        var snapshot = File.ReadAllText("initial-release.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_1_doc_produces_correct_output()
    {
        var generator = serviceProvider.GetRequiredService<IOpenApiDocumentGenerator>();
        var doc = await generator.GenerateAsync("Release 1.0");

        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);
        //await File.WriteAllTextAsync("release-1.json", json);

        var snapshot = File.ReadAllText("release-1.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_2_doc_produces_correct_output()
    {
        var generator = serviceProvider.GetRequiredService<IOpenApiDocumentGenerator>();
        var doc = await generator.GenerateAsync("Release 2.0");

        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);
        //await File.WriteAllTextAsync("release-2.json", json);

        var snapshot = File.ReadAllText("release-2.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }
}
