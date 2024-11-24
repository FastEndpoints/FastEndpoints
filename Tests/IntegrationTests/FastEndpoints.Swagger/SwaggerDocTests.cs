using FluentAssertions.Json; //this is highly important. the BeEquivalentTo() extension method from FluentAssertions main namespace doesn't work

namespace Swagger;

public class SwaggerDocTests(Fixture App) : TestBase<Fixture>
{
    //NOTE: the Verify snapshot testing doesn't seem to work in gh workflow for some reason
    //      so we're doing manual json file comparison. matching against verified json files in the project root vs latest generated json.
    //      to update the golden master (verified json files), just uncomment the UpdateSnapshot() calls and run the tests.
    //      don't forget to comment them out afterward. because if you don't you're always comparing against newly generated output.

    [Fact]
    public async Task initial_release_doc_produces_correct_output()
    {
        var doc = await App.DocGenerator.GenerateAsync("Initial Release");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        //await UpdateSnapshot("initial-release.json", json);

        var snapshot = await File.ReadAllTextAsync("initial-release.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_1_doc_produces_correct_output()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 1.0");
        var json = doc.ToJson();

        var currentDoc = JToken.Parse(json);

        //await UpdateSnapshot("release-1.json", json);

        var snapshot = await File.ReadAllTextAsync("release-1.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_2_doc_produces_correct_output()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var json = doc.ToJson();

        var currentDoc = JToken.Parse(json);

        //await UpdateSnapshot("release-2.json", json);

        var snapshot = await File.ReadAllTextAsync("release-2.json");
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.Should().BeEquivalentTo(snapshotDoc);
    }

    // ReSharper disable once UnusedMember.Local
    static async Task UpdateSnapshot(string jsonFileName, string jsonContent)
    {
        var destination = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", jsonFileName));

        await File.WriteAllTextAsync(destination, jsonContent);

        throw new OperationCanceledException($"Snapshots updated! Go ahead and comment out the {nameof(UpdateSnapshot)}() methods and re-run the tests!");
    }
}