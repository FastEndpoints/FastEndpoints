using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.OpenApi;

namespace OpenApi;

public class Fixture : AppFixture<Web.Program>
{
    static readonly SemaphoreSlim DocumentGenerationLock = new(1, 1);

    public HttpClient DocClient { get; set; } = default!;

    protected override ValueTask SetupAsync()
    {
        DocClient = CreateClient();

        return ValueTask.CompletedTask;
    }

    public async Task<string> GetDocumentJsonAsync(string documentName)
    {
        await DocumentGenerationLock.WaitAsync();

        try
        {
            var url = $"/openapi/{Uri.EscapeDataString(documentName)}.json";
            using var response = await DocClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }
        finally
        {
            DocumentGenerationLock.Release();
        }
    }

    public async Task<string> GetHttpFileContentAsync(string documentName, CancellationToken ct)
    {
        await DocumentGenerationLock.WaitAsync(ct);

        try
        {
            var normalizedDocumentName = documentName.ToLowerInvariant();
            var provider = Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(normalizedDocumentName);
            var doc = await provider.GetOpenApiDocumentAsync(ct);

            return HttpFileExporter.ToHttpFileContent(doc, normalizedDocumentName);
        }
        finally
        {
            DocumentGenerationLock.Release();
        }
    }
}
