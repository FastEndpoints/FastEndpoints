namespace TestCases.JsonArrayBindingToListOfModels;

public class Endpoint : Endpoint<List<Request>, List<Response>>
{
    public override void Configure()
    {
        Post("/test-cases/json-array-binding-to-list-of-models");
        AllowAnonymous();
    }

    public override Task HandleAsync(List<Request> r, CancellationToken c)
    {
        Response = r.Select(x => new Response { Name = x.Name }).ToList();
        return Task.CompletedTask;
    }
}