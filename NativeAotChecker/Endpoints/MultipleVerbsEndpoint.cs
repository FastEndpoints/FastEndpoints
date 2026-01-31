using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Single endpoint handling multiple HTTP verbs in AOT mode
public sealed class MultipleVerbsRequest
{
    public string Data { get; set; } = string.Empty;
    
    [QueryParam]
    public string QueryData { get; set; } = string.Empty;
}

public sealed class MultipleVerbsResponse
{
    public string HttpMethod { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string QueryData { get; set; } = string.Empty;
    public bool IsGet { get; set; }
    public bool IsPost { get; set; }
    public bool IsPut { get; set; }
}

public sealed class MultipleVerbsEndpoint : Endpoint<MultipleVerbsRequest, MultipleVerbsResponse>
{
    public override void Configure()
    {
        Verbs(Http.GET, Http.POST, Http.PUT);
        Routes("multiple-verbs-test");
        AllowAnonymous();
        SerializerContext<MultipleVerbsSerCtx>();
    }

    public override async Task HandleAsync(MultipleVerbsRequest req, CancellationToken ct)
    {
        var method = HttpContext.Request.Method;
        
        await Send.OkAsync(new MultipleVerbsResponse
        {
            HttpMethod = method,
            Data = req.Data,
            QueryData = req.QueryData,
            IsGet = method == "GET",
            IsPost = method == "POST",
            IsPut = method == "PUT"
        }, ct);
    }
}

[JsonSerializable(typeof(MultipleVerbsRequest))]
[JsonSerializable(typeof(MultipleVerbsResponse))]
public partial class MultipleVerbsSerCtx : JsonSerializerContext;
