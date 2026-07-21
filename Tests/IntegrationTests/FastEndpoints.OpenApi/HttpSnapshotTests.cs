namespace OpenApi;

public class HttpSnapshotTests(Fixture App) : TestBase<Fixture>
{
    //NOTE: matching against verified .http files in the project root vs latest generated content.
    //      to update the golden master (verified .http files), just set '_updateSnapshots = true' and run the tests.
    //      don't forget to 'false' afterward. because if you don't you're always comparing against newly generated output.
    static readonly bool _updateSnapshots = false;

    [Fact]
    public async Task release_0_doc()
    {
        var http = await App.GetHttpFileContentAsync("Initial Release", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-0.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-0.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    [Fact]
    public async Task release_1_doc()
    {
        var http = await App.GetHttpFileContentAsync("Release 1.0", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-1.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-1.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    [Fact]
    public async Task release_2_doc()
    {
        var http = await App.GetHttpFileContentAsync("Release 2.0", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-2.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-2.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    [Fact]
    public async Task release_versioning_v0()
    {
        var http = await App.GetHttpFileContentAsync("ReleaseVersioning - v0", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-versioning-v0.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v0.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    [Fact]
    public async Task release_versioning_v1()
    {
        var http = await App.GetHttpFileContentAsync("ReleaseVersioning - v1", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-versioning-v1.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v1.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    [Fact]
    public async Task release_versioning_v2()
    {
        var http = await App.GetHttpFileContentAsync("ReleaseVersioning - v2", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-versioning-v2.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v2.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    [Fact]
    public async Task release_versioning_v3()
    {
        var http = await App.GetHttpFileContentAsync("ReleaseVersioning - v3", Cancellation);

        if (await UpdateSnapshotIfEnabled("release-versioning-v3.http", http))
            Assert.Fail("Snapshot updated! Turn off snapshot updating to run tests...");

        var snapshot = await File.ReadAllTextAsync("release-versioning-v3.http", Cancellation);
        Assert.Equal(Normalize(snapshot), Normalize(http));
    }

    static string Normalize(string content)
        => content.Replace("\r\n", "\n");

    // ReSharper disable once UnusedMember.Local
    static async Task<bool> UpdateSnapshotIfEnabled(string httpFileName, string httpContent)
    {
        if (!_updateSnapshots)
            return false;

        var destination = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", httpFileName));
        await File.WriteAllTextAsync(destination, httpContent);

        return true;
    }
}