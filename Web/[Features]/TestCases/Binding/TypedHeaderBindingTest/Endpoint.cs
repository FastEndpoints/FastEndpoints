using Microsoft.Net.Http.Headers;

namespace TestCases.TypedHeaderBindingTest;

sealed class MyRequest : PlainTextRequest
{
    [FromHeader("Content-Disposition")]
    public ContentDispositionHeaderValue Disposition { get; set; }
}

sealed class MyEndpoint : Endpoint<MyRequest, string>
{
    public override void Configure()
    {
        Post("test-cases/typed-header-binding-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MyRequest r, CancellationToken c)
    {
        await SendAsync(r.Disposition.FileName.Value!);
    }
}