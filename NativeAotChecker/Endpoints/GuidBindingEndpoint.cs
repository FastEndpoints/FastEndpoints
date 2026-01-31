using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Guid binding in various locations in AOT mode
public sealed class GuidBindingRequest
{
    public Guid Id { get; set; }
    
    [QueryParam]
    public Guid? QueryGuid { get; set; }
    
    public List<Guid> GuidList { get; set; } = [];
    
    public Dictionary<string, Guid> GuidDict { get; set; } = new();
}

public sealed class GuidBindingResponse
{
    public Guid Id { get; set; }
    public Guid? QueryGuid { get; set; }
    public int GuidListCount { get; set; }
    public int GuidDictCount { get; set; }
    public Guid? FirstListGuid { get; set; }
    public bool GuidsBound { get; set; }
}

public sealed class GuidBindingEndpoint : Endpoint<GuidBindingRequest, GuidBindingResponse>
{
    public override void Configure()
    {
        Post("guid-binding/{Id}");
        AllowAnonymous();
        SerializerContext<GuidBindingSerCtx>();
    }

    public override async Task HandleAsync(GuidBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new GuidBindingResponse
        {
            Id = req.Id,
            QueryGuid = req.QueryGuid,
            GuidListCount = req.GuidList.Count,
            GuidDictCount = req.GuidDict.Count,
            FirstListGuid = req.GuidList.FirstOrDefault(),
            GuidsBound = req.Id != Guid.Empty
        }, ct);
    }
}

[JsonSerializable(typeof(GuidBindingRequest))]
[JsonSerializable(typeof(GuidBindingResponse))]
[JsonSerializable(typeof(List<Guid>))]
[JsonSerializable(typeof(Dictionary<string, Guid>))]
public partial class GuidBindingSerCtx : JsonSerializerContext;
