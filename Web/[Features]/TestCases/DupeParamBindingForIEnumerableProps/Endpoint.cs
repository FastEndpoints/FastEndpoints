namespace TestCases.DupeParamBindingForIEnumerableProps;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Get("/test-cases/dupe-param-binding-for-ienumerable-props");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        Response.Doubles = r.Doubles;
        Response.Dates = r.Dates;
        Response.Guids = r.Guids;
        Response.Ints = r.Ints;
        Response.Strings = r.Strings;
        Response.MoreStrings = r.MoreStrings;
        Response.Persons = r.Persons;
        return Task.CompletedTask;
    }
}