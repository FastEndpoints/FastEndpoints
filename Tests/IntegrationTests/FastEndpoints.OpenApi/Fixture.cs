using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.OpenApi;

namespace OpenApi;

public class Fixture : AppFixture<Web.Program>
{
    // serializes snapshot/helper fetches so parallel test classes do not pile onto the same
    // expensive generation. overlapping generation itself is safe (mutation bags are per OpenApiDocument).
    static readonly SemaphoreSlim _documentGenerationLock = new(1, 1);

    readonly Dictionary<string, string> _documentJson = new(StringComparer.Ordinal);

    public HttpClient DocClient { get; set; } = default!;

    protected override ValueTask SetupAsync()
    {
        DocClient = CreateClient();

        return ValueTask.CompletedTask;
    }

    // Only for unchanged-document inspection. Parse the string separately in each test.
    public async Task<string> GetCachedDocumentJsonAsync(string documentName)
    {
        await _documentGenerationLock.WaitAsync();

        try
        {
            if (!_documentJson.TryGetValue(documentName, out var json))
            {
                json = await ReadDocumentJsonAsync(documentName);
                _documentJson.Add(documentName, json);
            }

            return json;
        }
        finally
        {
            _documentGenerationLock.Release();
        }
    }

    // Keep generation fresh for stability and mutation-isolation tests.
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