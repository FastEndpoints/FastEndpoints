namespace TestCases.RouteBindingTest;

public class Endpoint : Endpoint<Request, Response>
{
    public ILogger<Endpoint> MyProperty; //this should be ignored by property binding

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/route-binding-test/{string}/{bool}/{int}/{long}/{double}/{decimal}");
        AllowAnonymous();
        DontThrowIfValidationFails();
        Options(x => x
            .WithName("RouteBindingTest")
            .Accepts<Request>("application/json", "test1/test1", "test2/test2"));
    }

    public override Task HandleAsync(Request r, CancellationToken t)
    {
        return SendAsync(new Response
        {
            Bool = r.Bool,
            Decimal = r.Decimal,
            Double = r.Double,
            FromBody = r.FromBody,
            Int = r.Int!.Value,
            Long = r.Long,
            String = r.String,
        });
    }
}
