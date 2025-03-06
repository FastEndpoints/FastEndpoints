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
    public Task PostProcessAsync(IPostProcessorContext<TReq, TRes> ctx, CancellationToken ct)
    {
        if (ctx.Request is Request r)
            r.PostProcRan = true;

        return Task.CompletedTask;
    }
}

public class Endpoint : Endpoint<Request, Request>
{
    public override void Configure()
    {
        Post("testcases/global-generic-processors");
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await SendAsync(req);
    }
}