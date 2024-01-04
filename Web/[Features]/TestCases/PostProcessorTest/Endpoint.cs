namespace TestCases.PostProcessorTest;

public class Request
{
    public int Id { get; set; }
}

public class ExceptionDetailsResponse
{
    public string Type { get; set; }
}

public class Processor : IPostProcessor<Request, ExceptionDetailsResponse>
{
    public async Task PostProcessAsync(IPostProcessorContext<Request, ExceptionDetailsResponse> context, CancellationToken ct)
    {
        if (!context.HasExceptionOccurred)
            return;

        context.MarkExceptionAsHandled();
        var error = context.ExceptionDispatchInfo.SourceException;
        await context.HttpContext.Response.SendAsync(
            new ExceptionDetailsResponse
            {
                Type = error.GetType().Name
            },
            412);
    }
}

public class Endpoint : Endpoint<Request, ExceptionDetailsResponse>
{
    public override void Configure()
    {
        Get("testcases/post-processor-handles-exception");
        AllowAnonymous();
        PostProcessor<Processor>();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
        => throw new NotImplementedException();
}

public class EpNoPostProcessor : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("testcases/post-processor-handles-exception/no-post-processor");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken c)
        => throw new NotImplementedException();
}