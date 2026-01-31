using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request implementing IPlainTextRequest interface
public class PlainTextAotRequest : IPlainTextRequest
{
    /// <summary>
    /// The raw body content is bound here
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Route parameter still works
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// Query parameter still works
    /// </summary>
    public string Format { get; set; } = string.Empty;
}

public class PlainTextAotResponse
{
    public int DocumentId { get; set; }
    public string Format { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public int ContentLength { get; set; }
    public int LineCount { get; set; }
    public int WordCount { get; set; }
    public bool PlainTextWorked { get; set; }
}

/// <summary>
/// Tests IPlainTextRequest interface for raw body content in AOT mode.
/// AOT ISSUE: Interface detection uses 'is' pattern matching.
/// IPlainTextRequest discovery at startup uses reflection.
/// Content property setter invocation may use reflection.
/// </summary>
public class PlainTextRequestEndpoint : Endpoint<PlainTextAotRequest, PlainTextAotResponse>
{
    public override void Configure()
    {
        Post("plain-text-test/{DocumentId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PlainTextAotRequest req, CancellationToken ct)
    {
        var content = req.Content ?? string.Empty;
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var words = content.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);

        await Send.OkAsync(new PlainTextAotResponse
        {
            DocumentId = req.DocumentId,
            Format = req.Format,
            RawContent = content.Length > 100 ? content[..100] + "..." : content,
            ContentLength = content.Length,
            LineCount = lines.Length,
            WordCount = words.Length,
            PlainTextWorked = content.Length > 0
        });
    }
}
