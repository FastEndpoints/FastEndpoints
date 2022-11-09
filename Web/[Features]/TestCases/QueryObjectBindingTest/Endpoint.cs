namespace TestCases.QueryObjectBindingTest;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/test-cases/query-object-binding-test");
        AllowAnonymous();
        DontThrowIfValidationFails();
        Options(x => x
            .WithName("QueryObjectBindingTest"));
        Summary(s =>
        {
            s.Description = "descr";
            s.Summary = "summary";
            s.ResponseParam<Response>(200, s => s.String, "Some string property");
            s.ResponseParam<Response>(s => s.Bool, "Some bool property");
        });
        //SerializerContext(ApiSerializerContext.Default);
        //next: fix sourcegen support here + FastEndpoints.Generator for .net 7
    }

    public override Task HandleAsync(Request r, CancellationToken t)
    {
        return SendAsync(new Response
        {
            Bool = r.Bool,
            Double = r.Double,
            Int = r.Int!.Value,
            Long = r.Long,
            String = r.String,
            Person = r.Person,
            Enum = r.Enum
        });
    }
}
