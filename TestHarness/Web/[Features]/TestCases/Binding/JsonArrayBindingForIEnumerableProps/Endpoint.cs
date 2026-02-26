namespace TestCases.JsonArrayBindingForIEnumerableProps;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/test-cases/json-array-binding-for-ienumerable-props");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        Response.Doubles = r.Doubles;
        Response.Dates = r.Dates;
        Response.Guids = r.Guids;
        Response.Ints = r.Ints;
        Response.Steven = r.Steven;
        Response.Dict = r.Dict;
        return Task.CompletedTask;
    }
}