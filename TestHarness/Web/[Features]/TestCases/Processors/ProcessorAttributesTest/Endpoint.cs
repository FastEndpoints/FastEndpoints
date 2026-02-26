using Microsoft.AspNetCore.Authorization;

namespace TestCases.ProcessorAttributesTest;

sealed class PreProc1 : IPreProcessor<Request>
{
    public Task PreProcessAsync(IPreProcessorContext<Request> ctx, CancellationToken c)
    {
        ctx.Request?.Values.Add("one");

        return Task.CompletedTask;
    }
}

sealed class PreProc2 : IPreProcessor<Request>
{
    public Task PreProcessAsync(IPreProcessorContext<Request> ctx, CancellationToken c)
    {
        ctx.Request?.Values.Add("two");

        return Task.CompletedTask;
    }
}

sealed class PostProc1 : IPostProcessor<Request, List<string>>
{
    public Task PostProcessAsync(IPostProcessorContext<Request, List<string>> ctx, CancellationToken c)
    {
        ctx.MarkExceptionAsHandled();

        ctx.Request?.Values.Add("three");

        return Task.CompletedTask;
    }
}

sealed class PostProc2 : IPostProcessor<Request, List<string>>
{
    public async Task PostProcessAsync(IPostProcessorContext<Request, List<string>> ctx, CancellationToken c)
    {
        ctx.MarkExceptionAsHandled();

        ctx.Request?.Values.Add("four");

        await ctx.HttpContext.Response.SendAsync(ctx.Request!.Values);
    }
}

sealed class Request
{
    public List<string> Values { get; set; }
}

[HttpPost("/test-cases/processor-attributes-test"),
 AllowAnonymous,
 PreProcessor<PreProc1>,
 PreProcessor<PreProc2>,
 PostProcessor<PostProc1>,
 PostProcessor<PostProc2>]
sealed class Endpoint : Endpoint<Request, List<string>>
{
    public override Task HandleAsync(Request r, CancellationToken c)
        => throw new NotImplementedException();
}