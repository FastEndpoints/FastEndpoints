using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request/Response DTOs
public class DynamicResponseRequest
{
    public string ResponseType { get; set; } = string.Empty; // "object", "dynamic", "expando"
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Tests dynamic/object response types in AOT mode.
/// AOT ISSUE: Dynamic types use DLR which is reflection-heavy.
/// ExpandoObject serialization uses runtime type inspection.
/// Object-typed responses need runtime type discovery for JSON.
/// </summary>
public class DynamicResponseEndpoint : Endpoint<DynamicResponseRequest, object>
{
    public override void Configure()
    {
        Get("dynamic-response");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DynamicResponseRequest req, CancellationToken ct)
    {
        object response = req.ResponseType switch
        {
            "expando" => CreateExpandoResponse(req),
            "anonymous" => new { Key = req.Key, Value = req.Value, Type = "anonymous" },
            _ => new Dictionary<string, object>
            {
                ["key"] = req.Key,
                ["value"] = req.Value,
                ["type"] = "dictionary"
            }
        };

        await Send.OkAsync(response);
    }

    private static dynamic CreateExpandoResponse(DynamicResponseRequest req)
    {
        dynamic expando = new System.Dynamic.ExpandoObject();
        expando.Key = req.Key;
        expando.Value = req.Value;
        expando.Type = "expando";
        return expando;
    }
}
