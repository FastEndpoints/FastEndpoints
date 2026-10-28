namespace TestCases.OpenApi.NestedGroupTags;

sealed class FirstGroup : Group
{
    public FirstGroup()
        => Configure("first", ep => ep.Description(b => b.WithTags("First")));
}

sealed class MiddleGroup : SubGroup<FirstGroup>
{
    public MiddleGroup()
        => Configure("middle", ep => ep.Description(b => b.WithTags("Middle")));
}

sealed class LastGroup : SubGroup<MiddleGroup>
{
    public LastGroup()
        => Configure("last", ep => ep.Description(b => b.WithTags("Last")));
}

sealed class Endpoint : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/item");
        Group<LastGroup>();
        AllowAnonymous();
        Tags("nested_group_tags");
        Description(b => b.WithTags("Endpoint"));
    }

    public override Task HandleAsync(CancellationToken ct)
        => Send.OkAsync(ct);
}
