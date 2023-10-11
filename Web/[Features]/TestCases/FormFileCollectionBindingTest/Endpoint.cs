namespace TestCases.FormFileBindingTest;

class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Post("test-cases/form-file-collection-binding");
        AllowFileUploads();
    }

    public override async Task HandleAsync(Request r, CancellationToken ct)
    {
        await SendAsync(
            new()
            {
                CarNames = new() { r.Cars.Select(f => f.FileName).ToArray() },
                JetNames = new() { r.Jets.Select(f => f.FileName).ToArray() },
                File1Name = r.File1.FileName,
                File2Name = r.File2.FileName
            });
    }
}