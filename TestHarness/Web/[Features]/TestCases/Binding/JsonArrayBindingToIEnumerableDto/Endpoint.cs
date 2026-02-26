namespace TestCases.JsonArrayBindingToIEnumerableDto;

public class Endpoint : Endpoint<Request, List<Response>>
{
    public override void Configure()
    {
        Post("/test-cases/json-array-binding-to-ienumerable-dto");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        Response = r.Select(x => new Response { Id = x.Id, Name = x.Name }).ToList();
        return Task.CompletedTask;
    }
}