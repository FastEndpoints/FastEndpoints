namespace TestCases.MetadataRegistrationTest;

class SomeObject
{
    public int Id { get; set; }
    public bool Yes { get; set; }
}

public class Endpoint : EndpointWithoutRequest<int>
{
    public override void Configure()
    {
        Get("/test-cases/endpoint-metadata-reg-test");
        Metadata(
            new SomeObject { Id = 1, Yes = true },
            new SomeObject { Id = 2, Yes = false });
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var someObjectCount = HttpContext.GetEndpoint()?.Metadata.OfType<SomeObject>().Count(o => o.Yes);
        await Send.OkAsync(someObjectCount ?? 0, c);
    }
}