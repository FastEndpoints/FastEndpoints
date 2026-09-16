using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.OpenApi;

namespace OpenApi;

public class Fixture : AppFixture<Web.Program>
{
    // serializes snapshot/helper fetches so parallel test classes do not pile onto the same
    // expensive generation. overlapping generation itself is safe (mutation bags are per OpenApiDocument).
    static readonly SemaphoreSlim _documentGenerationLock = new(1, 1);

    public HttpClient DocClient { get; set; } = default!;

    protected override ValueTask SetupAsync()
    {
        DocClient = CreateClient();

        return ValueTask.CompletedTask;
    }

    public async Task<string> GetDocumentJsonAsync(string documentName)
    {
        await _documentGenerationLock.WaitAsync();

        try
        {
            return await ReadDocumentJsonAsync(documentName);
        }
        finally
        {
            _documentGenerationLock.Release();
        }
    }

    public Task<string> GetDocumentJsonUnlockedAsync(string documentName)
        => ReadDocumentJsonAsync(documentName);

    public async Task<string> GetHttpFileContentAsync(string documentName, CancellationToken ct)
    {
        await _documentGenerationLock.WaitAsync(ct);

        try
        {
            var normalizedDocumentName = documentName.ToLowerInvariant();
            var provider = Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
            var doc = await provider.GetOpenApiDocumentAsync(ct);

            return HttpFileExporter.ToHttpFileContent(doc);
        }
        finally
        {
            _documentGenerationLock.Release();
        }
    }

    async Task<string> ReadDocumentJsonAsync(string documentName)
    {
        var url = $"/openapi/{Uri.EscapeDataString(documentName)}.json";
        using var response = await DocClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}