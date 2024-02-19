namespace TestCases.QueryObjectWithObjectsArrayBindingTest;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/test-cases/query-arrays-of-objects-binding-test");
        AllowAnonymous();
        DontThrowIfValidationFails();
        Options(x => x
            .WithName("QueryArraysOfObjectsBindingTest"));
        Summary(s =>
        {
            s.Description = "descr";
            s.Summary = "summary";
        });
    }

    public override Task HandleAsync(Request r, CancellationToken t)
    {

        return SendAsync(new Response
        {
            Person = r.Person
        });
    }
}
