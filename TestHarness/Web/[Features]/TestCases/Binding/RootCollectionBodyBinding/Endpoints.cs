namespace TestCases.RootCollectionBodyBinding;

public sealed class Item
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CountResponse
{
    public int Count { get; set; }
}

sealed class GetListEndpoint : Endpoint<List<Item>, CountResponse>
{
    public override void Configure()
    {
        Get("/test-cases/root-collection-body-binding/list");
        AllowAnonymous();
        Options(b => b.ExcludeFromDescription());
    }

    public override Task HandleAsync(List<Item> r, CancellationToken ct)
        => Send.OkAsync(new() { Count = r.Count }, ct);
}

sealed class HeadListEndpoint : Endpoint<List<Item>>
{
    public override void Configure()
    {
        Head("/test-cases/root-collection-body-binding/list");
        AllowAnonymous();
        Options(b => b.ExcludeFromDescription());
    }

    public override Task HandleAsync(List<Item> r, CancellationToken ct)
    {
        HttpContext.Response.Headers["x-bound-count"] = r.Count.ToString();

        return Send.OkAsync(ct);
    }
}
