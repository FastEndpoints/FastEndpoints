namespace TestCases.RouteBindingTest;

public class Endpoint : Endpoint<Request, Response>
{
    public ILogger<Endpoint> logger; //this should be ignored by property injection because it doesn't have getter/setter

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/route-binding-test/{string}/{bool}/{int}/{long}/{double}/{decimal}");
        AllowAnonymous();
        DontThrowIfValidationFails();
        Options(x => x
            .WithName("RouteBindingTest")
            .Accepts<Request>("application/json", "test1/test1", "test2/test2"));
        Summary(s =>
        {
            s.Description = "descr";
            s.Summary = "summary";
            s.RequestParam(r => r.FromBody, "overriden from body comment");
        });
    }

    public override Task HandleAsync(Request r, CancellationToken t)
    {
        Logger.LogWarning("ok");

        if (logger != null) ThrowError("property injection failed us!");

        return SendAsync(new Response
        {
            Bool = r.Bool,
            Decimal = r.DecimalNumber,
            Double = r.Double,
            FromBody = r.FromBody,
            Int = r.Int!.Value,
            Long = r.Long,
            String = r.String,
            Blank = r.Blank,
            Url = r.Url?.ToString(),
            Custom = r.Custom,
            CustomList = r.CustomList,
            Person = r.Person
        });
    }
}
