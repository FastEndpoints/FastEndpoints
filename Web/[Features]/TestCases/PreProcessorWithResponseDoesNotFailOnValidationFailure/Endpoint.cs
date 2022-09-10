namespace TestCases.PreProcessorWithResponseDoesNotFailOnValidationFailure;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/pre-processor-with-response-does-not-fail-validation-failure");
        PreProcessors(new MyPreProcessor());
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        //a validaiton failure will occur but pre processor should run
        return SendAsync(Response);
    }
}