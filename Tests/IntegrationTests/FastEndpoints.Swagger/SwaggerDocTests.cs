using FluentAssertions.Json;

namespace Swagger;

public class SwaggerDocTests : TestClass<Fixture>
{
    public SwaggerDocTests(Fixture f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task initial_release_doc_produces_correct_output()
    {
        var doc = await Fixture.DocGenerator.GenerateAsync("Initial Release");

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
        var doc = await Fixture.DocGenerator.GenerateAsync("Release 1.0");

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
        var doc = await Fixture.DocGenerator.GenerateAsync("Release 2.0");

        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        //await File.WriteAllTextAsync("release-2.json", json);

        var snapshot = File.ReadAllText("release-2.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }
}