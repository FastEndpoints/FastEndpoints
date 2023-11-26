using FluentAssertions;

namespace Swagger;

//NOTE: the Verify snapshot testing doesn't seem to work in gh workflow for some reason
//      so we're doing manual json file comparison.
//      to update the golden master (verified json files), copy them from the bin folder in to the project root.
//      uncomment the File.WriteAllTextAsync() methods to put new golden masters in the bin folder.

//[UsesVerify]
public class SwaggerDocTests(Fixture f, ITestOutputHelper o) : TestClass<Fixture>(f, o)
{
    [Fact]
    public async Task initial_release_doc_produces_correct_output()
    {
        var doc = await Fixture.DocGenerator.GenerateAsync("Initial Release");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        //await File.WriteAllTextAsync("initial-release.json", json);

        var snapshot = await File.ReadAllTextAsync("initial-release.json");
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

        var snapshot = await File.ReadAllTextAsync("release-1.json");
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

        var snapshot = await File.ReadAllTextAsync("release-2.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }
}