using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Collection binding from query parameters in AOT mode
public sealed class CollectionBindingRequest
{
    [QueryParam]
    public List<int> Ids { get; set; } = [];

    [QueryParam]
    public string[] Names { get; set; } = [];

    [QueryParam]
    public List<Guid> Guids { get; set; } = [];

    [QueryParam]
    public ProductCategory[] Categories { get; set; } = [];
}

public sealed class CollectionBindingResponse
{
    public List<int> Ids { get; set; } = [];
    public int IdCount { get; set; }
    public string[] Names { get; set; } = [];
    public int NameCount { get; set; }
    public List<Guid> Guids { get; set; } = [];
    public int GuidCount { get; set; }
    public ProductCategory[] Categories { get; set; } = [];
    public int CategoryCount { get; set; }
}

public sealed class CollectionBindingEndpoint : Endpoint<CollectionBindingRequest, CollectionBindingResponse>
{
    public override void Configure()
    {
        Get("collection-binding");
        AllowAnonymous();
        SerializerContext<CollectionBindingSerCtx>();
    }

    public override async Task HandleAsync(CollectionBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new CollectionBindingResponse
        {
            Ids = req.Ids,
            IdCount = req.Ids.Count,
            Names = req.Names,
            NameCount = req.Names.Length,
            Guids = req.Guids,
            GuidCount = req.Guids.Count,
            Categories = req.Categories,
            CategoryCount = req.Categories.Length
        }, ct);
    }
}

[JsonSerializable(typeof(CollectionBindingRequest))]
[JsonSerializable(typeof(CollectionBindingResponse))]
[JsonSerializable(typeof(ProductCategory))]
public partial class CollectionBindingSerCtx : JsonSerializerContext;
