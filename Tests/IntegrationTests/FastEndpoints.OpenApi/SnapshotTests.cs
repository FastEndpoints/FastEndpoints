namespace OpenApi;

public class SnapshotTests(Fixture App) : TestBase<Fixture>
{
    //NOTE: matching against verified json files in the project root vs latest generated json.
    //      to update the golden master (verified json files), just set '_updateSnapshots = true' and run the tests.
    //      don't forget to 'false' afterward. because if you don't you're always comparing against newly generated output.
    static readonly bool _updateSnapshots = false;

    [Fact]
    public async Task release_0_doc()
    {
        var json = await App.GetDocumentJsonAsync("Initial Release");

        if (await UpdateSnapshotIfEnabled("release-0.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-0.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    [Fact]
    public async Task release_1_doc()
    {
        var json = await App.GetDocumentJsonAsync("Release 1.0");

        if (await UpdateSnapshotIfEnabled("release-1.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-1.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    [Fact]
    public async Task release_2_doc()
    {
        var json = await App.GetDocumentJsonAsync("Release 2.0");

        if (await UpdateSnapshotIfEnabled("release-2.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-2.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    [Fact]
    public async Task release_versioning_v0()
    {
        var json = await App.GetDocumentJsonAsync("ReleaseVersioning - v0");

        if (await UpdateSnapshotIfEnabled("release-versioning-v0.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v0.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    [Fact]
    public async Task release_versioning_v1()
    {
        var json = await App.GetDocumentJsonAsync("ReleaseVersioning - v1");

        if (await UpdateSnapshotIfEnabled("release-versioning-v1.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v1.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    [Fact]
    public async Task release_versioning_v2()
    {
        var json = await App.GetDocumentJsonAsync("ReleaseVersioning - v2");

        if (await UpdateSnapshotIfEnabled("release-versioning-v2.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v2.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    [Fact]
    public async Task release_versioning_v3()
    {
        var json = await App.GetDocumentJsonAsync("ReleaseVersioning - v3");

        if (await UpdateSnapshotIfEnabled("release-versioning-v3.json", json))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v3.json", Cancellation);
        JsonSnapshotComparer.AssertMatches(json, snapshot);
    }

    // ReSharper disable once UnusedMember.Local
    static async Task<bool> UpdateSnapshotIfEnabled(string jsonFileName, string jsonContent)
    {
        if (!_updateSnapshots)
            return false;

        var destination = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", jsonFileName));
        await File.WriteAllTextAsync(destination, jsonContent);

        return true;
    }
}