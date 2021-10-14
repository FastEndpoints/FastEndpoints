namespace Uploads.Image.Save;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("uploads/image/save");
        AllowFileUploads();
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken ct)
    {
        if (Files.Count > 0)
        {
            var file = Files[0];
            return SendStreamAsync(file.OpenReadStream(), "test.png", file.Length, "image/png", ct);
        }

        return SendNoContentAsync();
    }
}
