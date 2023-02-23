namespace TestCases.ByteArrayQueryParamBindingTest;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/test-cases/byte-array-query-param-binding-test");
        AllowAnonymous();
        DontThrowIfValidationFails();
        Options(x => x
            .WithName("ByteArrayQueryParamBindingTest"));
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
            Timestamp = r.Timestamp,
            ObjectWithByteArrays = r.ObjectWithByteArrays
        });
    }
}
