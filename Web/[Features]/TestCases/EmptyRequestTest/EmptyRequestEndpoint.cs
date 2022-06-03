namespace TestCases.EmptyRequestTest;

public class EmptyRequestEndpoint: Endpoint<EmptyRequest, EmptyResponse>
{
    public override void Configure()
    {
        Verbs(Http.GET);
        Routes("/test-cases/empty-request-test");
    }
    
    public async override Task HandleAsync(EmptyRequest req, CancellationToken ct) => await SendOkAsync(ct);
}