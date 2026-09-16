namespace OpenApi;

public class ConcurrentDocumentGenerationTests(Fixture App) : TestBase<Fixture>
{
    [Fact]
    public async Task overlapping_document_requests_do_not_corrupt_output()
    {
        const string documentName = "Release 2.0";
        const int concurrency = 16;

        var baseline = await App.GetDocumentJsonUnlockedAsync(documentName);
        var baselinePathCount = CountPaths(baseline);
        baselinePathCount.ShouldBeGreaterThan(0);

        var jsons = await Task.WhenAll(Enumerable.Range(0, concurrency).Select(_ => App.GetDocumentJsonUnlockedAsync(documentName)));

        foreach (var json in jsons)
        {
            CountPaths(json).ShouldBe(baselinePathCount);
            JsonSnapshotComparer.AssertMatches(json, baseline);
        }
    }

    static int CountPaths(string json)
        => JsonNode.Parse(json)?["paths"]?.AsObject().Count ?? 0;
}
