namespace NativeAotChecker.Endpoints.Json;

public sealed class JsonPostRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public sealed class JsonPostResponse
{
    public string Message { get; set; }
}

public sealed class JsonPostEndpoint : Endpoint<JsonPostRequest, JsonPostResponse>
{
    public override void Configure()
    {
        Post("json-post");
        AllowAnonymous();
        Summary(s => s.Summary = "this is the summary of this endpoint");
    }

    public override async Task HandleAsync(JsonPostRequest r, CancellationToken c)
    {
        await Send.OkAsync(new() { Message = $"Hello {r.FirstName} {r.LastName}!" });
    }
}