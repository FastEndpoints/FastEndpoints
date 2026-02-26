namespace TestCases.GlobalGenericProcessorTest;

public class Request
{
    public bool PreProcRan { get; set; }
    public bool PostProcRan { get; set; }
}

sealed class GlobalGenericPreProcessor<TReq> : IPreProcessor<TReq>
{
    public Task PreProcessAsync(IPreProcessorContext<TReq> ctx, CancellationToken ct)
    {
        if (ctx.Request is Request r)
            r.PreProcRan = true;

        return Task.CompletedTask;
    }
}

public class GlobalGenericPostProcessor<TReq, TRes> : IPostProcessor<TReq, TRes>
{
    public async Task PostProcessAsync(IPostProcessorContext<TReq, TRes> ctx, CancellationToken ct)
    {
        if (ctx.Request is Request r && !ctx.HttpContext.ResponseStarted())
        {
            r.PostProcRan = true;
            await ctx.HttpContext.Response.SendAsync(r, cancellation: ct);
        }
    }
}

public class Endpoint : Endpoint<Request, Request>
{
    public override void Configure()
    {
        Post("testcases/global-generic-processors");
        AllowAnonymous();
        DontAutoSendResponse();
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
        => Task.CompletedTask;
}