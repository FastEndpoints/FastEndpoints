namespace TestCases.PreProcessorIsRunOnValidationFailure;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/test-cases/pre-processor-is-run-on-validation-failure");
        PreProcessors(new MyPreProcessor());
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        //a validaiton failure will occur but pre processor should run
        return SendAsync(Response);
    }
}