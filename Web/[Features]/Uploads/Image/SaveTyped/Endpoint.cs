namespace Uploads.Image.SaveTyped;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.POST, Http.PUT);
        AllowAnonymous(Http.POST);
        Routes("uploads/image/save-typed");
        AccessControl("Image_Update");
        Permissions(Allow.Image_Update);
        Claims(Claim.AdminID);
        AllowFileUploads();
        Options(
            b => b.Produces(200, typeof(string), "image/png", "test/image")
                  .Produces(204, typeof(string), "text/plain", "test/notcontent"));
    }

    public override async Task HandleAsync(Request r, CancellationToken ct)
    {
        if (r.File1.Length > 0 && r.File2.Length > 0 && r.File3?.Length > 0)
        {
            await SendStreamAsync(r.File1.OpenReadStream(), "test.png", r.File1.Length, "image/png", cancellation: ct);

            return;
        }

        await SendNoContentAsync();
    }
}