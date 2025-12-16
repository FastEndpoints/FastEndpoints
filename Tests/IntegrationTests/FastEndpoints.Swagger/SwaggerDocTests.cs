namespace Swagger;

public class SwaggerDocTests(Fixture App) : TestBase<Fixture>
{
    //NOTE: the Verify snapshot testing doesn't seem to work in gh workflow for some reason
    //      so we're doing manual json file comparison. matching against verified json files in the project root vs latest generated json.
    //      to update the golden master (verified json files), just set '_updateSnapshots = true' and run the tests.
    //      don't forget to 'false' afterward. because if you don't you're always comparing against newly generated output.

    static readonly bool _updateSnapshots = false;

    [Fact]
    public async Task release_0_doc()
    {
        var doc = await App.DocGenerator.GenerateAsync("Initial Release");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-0.json", json);

        var snapshot = await File.ReadAllTextAsync("release-0.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_1_doc()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 1.0");
        var json = doc.ToJson();

        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-1.json", json);

        var snapshot = await File.ReadAllTextAsync("release-1.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_2_doc()
    {
        var doc = await App.DocGenerator.GenerateAsync("Release 2.0");
        var json = doc.ToJson();

        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-2.json", json);

        var snapshot = await File.ReadAllTextAsync("release-2.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }

    // ReSharper disable once UnusedMember.Local
    static async Task UpdateSnapshotIfEnabled(string jsonFileName, string jsonContent)
    {
        if (!_updateSnapshots)
            return;

        var destination = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", jsonFileName));

        await File.WriteAllTextAsync(destination, jsonContent);

        throw new OperationCanceledException($"Snapshots updated! Go ahead and comment out the {nameof(UpdateSnapshotIfEnabled)}() methods and re-run the tests!");
    }

    [Fact]
    public async Task release_versioning_v0()
    {
        var doc = await App.DocGenerator.GenerateAsync("ReleaseVersioning - v0");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-versioning-v0.json", json);

        var snapshot = await File.ReadAllTextAsync("release-versioning-v0.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_versioning_v1()
    {
        var doc = await App.DocGenerator.GenerateAsync("ReleaseVersioning - v1");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-versioning-v1.json", json);

        var snapshot = await File.ReadAllTextAsync("release-versioning-v1.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_versioning_v2()
    {
        var doc = await App.DocGenerator.GenerateAsync("ReleaseVersioning - v2");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-versioning-v2.json", json);

        var snapshot = await File.ReadAllTextAsync("release-versioning-v2.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }

    [Fact]
    public async Task release_versioning_v3()
    {
        var doc = await App.DocGenerator.GenerateAsync("ReleaseVersioning - v3");
        var json = doc.ToJson();
        var currentDoc = JToken.Parse(json);

        await UpdateSnapshotIfEnabled("release-versioning-v3.json", json);

        var snapshot = await File.ReadAllTextAsync("release-versioning-v3.json", Cancellation);
        var snapshotDoc = JToken.Parse(snapshot);

        currentDoc.ShouldBeEquivalentTo(snapshotDoc);
    }
}