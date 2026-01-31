using FastEndpoints;
using System.Text.Json;

namespace NativeAotChecker.Endpoints;

// Request with JsonDocument/JsonElement
public class JsonDocumentRequest
{
    public JsonDocument? Document { get; set; }
    public JsonElement Element { get; set; }
    public string RawJson { get; set; } = string.Empty;
}

public class JsonDocumentResponse
{
    public int PropertyCount { get; set; }
    public List<string> PropertyNames { get; set; } = [];
    public string ElementKind { get; set; } = string.Empty;
    public bool JsonDocumentWorked { get; set; }
}

/// <summary>
/// Tests JsonDocument and JsonElement handling in AOT mode.
/// AOT ISSUE: JsonDocument parsing is dynamic by nature.
/// JsonElement property enumeration uses runtime type inspection.
/// GetProperty by string name is reflection-like operation.
/// </summary>
public class JsonDocumentEndpoint : Endpoint<JsonDocumentRequest, JsonDocumentResponse>
{
    public override void Configure()
    {
        Post("json-document-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JsonDocumentRequest req, CancellationToken ct)
    {
        var propertyNames = new List<string>();
        int propertyCount = 0;

        if (req.Document != null)
        {
            foreach (var prop in req.Document.RootElement.EnumerateObject())
            {
                propertyNames.Add(prop.Name);
                propertyCount++;
            }
        }

        await Send.OkAsync(new JsonDocumentResponse
        {
            PropertyCount = propertyCount,
            PropertyNames = propertyNames,
            ElementKind = req.Element.ValueKind.ToString(),
            JsonDocumentWorked = propertyCount > 0
        });
    }
}

/// <summary>
/// Tests raw JSON string handling in AOT mode.
/// </summary>
public class RawJsonEndpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("raw-json-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Read raw JSON from body
        using var reader = new StreamReader(HttpContext.Request.Body);
        var rawJson = await reader.ReadToEndAsync(ct);

        // Parse and re-serialize
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var response = new
        {
            ReceivedLength = rawJson.Length,
            RootKind = root.ValueKind.ToString(),
            PropertyCount = root.ValueKind == JsonValueKind.Object 
                ? root.EnumerateObject().Count() 
                : 0,
            RawJsonWorked = true
        };

        await HttpContext.Response.WriteAsJsonAsync(response, ct);
    }
}
