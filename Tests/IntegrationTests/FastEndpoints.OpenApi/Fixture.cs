namespace OpenApi;

public class Fixture : AppFixture<Web.Program>
{
    public HttpClient DocClient { get; set; } = default!;

    protected override ValueTask SetupAsync()
    {
        DocClient = CreateClient();

        return ValueTask.CompletedTask;
    }

    public async Task<string> GetDocumentJsonAsync(string documentName)
    {
        var url = $"/openapi/{Uri.EscapeDataString(documentName)}.json";
        var response = await DocClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
}
