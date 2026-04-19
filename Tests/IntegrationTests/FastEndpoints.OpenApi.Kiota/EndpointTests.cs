namespace OpenApi.Kiota.Tests;

[Trait("ExcludeInCiCd", "Yes")]
public sealed class EndpointTests(Fixture app) : TestBase<Fixture>
{
    [Fact]
    public async Task api_client_endpoint_returns_zip_archive()
    {
        var response = await app.Client.GetAsync("/api-client", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/octet-stream");
        response.Content.Headers.ContentDisposition?.FileNameStar.ShouldBe("HarnessApiClient.zip");

        var archiveBytes = await response.Content.ReadAsByteArrayAsync(Cancellation);
        using var stream = new MemoryStream(archiveBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

        archive.Entries.Count.ShouldBeGreaterThan(0);
        archive.Entries.Any(e => e.FullName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public async Task api_client_endpoint_handles_concurrent_requests()
    {
        var requests = Enumerable.Range(0, 3).Select(_ => app.Client.GetAsync("/api-client", Cancellation));
        var responses = await Task.WhenAll(requests);

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var archiveBytes = await response.Content.ReadAsByteArrayAsync(Cancellation);
            archiveBytes.Length.ShouldBeGreaterThan(0);
        }
    }
}
