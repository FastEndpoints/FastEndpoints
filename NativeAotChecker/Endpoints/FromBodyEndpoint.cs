using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: [FromBody] attribute for explicit body binding in AOT mode
public sealed class FromBodyOuterRequest
{
    [QueryParam]
    public string QueryValue { get; set; } = string.Empty;

    [FromBody]
    public FromBodyInnerData Body { get; set; } = new();
}

public sealed class FromBodyInnerData
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public List<string> Tags { get; set; } = [];
}

public sealed class FromBodyResponse
{
    public string QueryValue { get; set; } = string.Empty;
    public string BodyName { get; set; } = string.Empty;
    public int BodyValue { get; set; }
    public int TagCount { get; set; }
    public bool BodyWasBound { get; set; }
}

public sealed class FromBodyEndpoint : Endpoint<FromBodyOuterRequest, FromBodyResponse>
{
    public override void Configure()
    {
        Post("from-body-test");
        AllowAnonymous();
        SerializerContext<FromBodySerCtx>();
    }

    public override async Task HandleAsync(FromBodyOuterRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new FromBodyResponse
        {
            QueryValue = req.QueryValue,
            BodyName = req.Body.Name,
            BodyValue = req.Body.Value,
            TagCount = req.Body.Tags.Count,
            BodyWasBound = !string.IsNullOrEmpty(req.Body.Name) || req.Body.Value > 0
        }, ct);
    }
}

[JsonSerializable(typeof(FromBodyOuterRequest))]
[JsonSerializable(typeof(FromBodyInnerData))]
[JsonSerializable(typeof(FromBodyResponse))]
public partial class FromBodySerCtx : JsonSerializerContext;
