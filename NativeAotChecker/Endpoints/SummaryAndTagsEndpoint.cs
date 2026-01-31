using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Summary, Description, and Tags configuration in AOT mode
public sealed class DocumentedRequest
{
    /// <summary>
    /// The user's full name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// A numeric identifier
    /// </summary>
    public int Id { get; set; }
}

public sealed class DocumentedResponse
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// This endpoint demonstrates metadata configuration for AOT
/// </summary>
public sealed class SummaryAndTagsEndpoint : Endpoint<DocumentedRequest, DocumentedResponse>
{
    public override void Configure()
    {
        Post("documented-endpoint");
        AllowAnonymous();
        SerializerContext<DocumentedSerCtx>();

        Summary(s =>
        {
            s.Summary = "A documented endpoint for testing";
            s.Description = "This endpoint is used to test that Summary and Tags work in AOT mode";
            s.ExampleRequest = new DocumentedRequest { Name = "John Doe", Id = 123 };
            s.Response<DocumentedResponse>(200, "Successful response");
            s.Response(400, "Bad request");
        });

        Tags("AOT", "Documentation", "Test");
    }

    public override async Task HandleAsync(DocumentedRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DocumentedResponse
        {
            Name = req.Name,
            Id = req.Id,
            Message = $"Hello {req.Name} (ID: {req.Id})"
        }, ct);
    }
}

// Test: Tags only endpoint
public sealed class TagsOnlyEndpoint : EndpointWithoutRequest<DocumentedResponse>
{
    public override void Configure()
    {
        Get("tags-only-endpoint");
        AllowAnonymous();
        SerializerContext<DocumentedSerCtx>();
        Tags("Category1", "Category2");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new DocumentedResponse
        {
            Name = "Tags Test",
            Id = 0,
            Message = "This endpoint has tags configured"
        }, ct);
    }
}

[JsonSerializable(typeof(DocumentedRequest))]
[JsonSerializable(typeof(DocumentedResponse))]
public partial class DocumentedSerCtx : JsonSerializerContext;
