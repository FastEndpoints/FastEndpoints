namespace TestCases.NestedFromFormBindingTest;

sealed class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("api/test-cases/nested-fromform-binding-test");
        AllowAnonymous();
        AllowFileUploads();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        await Send.OkAsync(new Response
        {
            Name = req.Data.Name,
            FileName = req.Data.File.FileName
        });
    }
}