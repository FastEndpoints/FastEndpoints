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
}
