namespace Uploads.Image.Save;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.POST, Http.PUT);
        AllowAnonymous(Http.POST);
        Routes("uploads/image/save");
        Permissions(Allow.Image_Update);
        Claims(Claim.AdminID);
        AllowFileUploads();
        Options(b => b
            .Accepts<Request>("multipart/form-data"));
    }

    public override Task HandleAsync(Request r, CancellationToken ct)
    {
        if (Files.Count > 0)
        {
            var file = Files[0];
            return SendStreamAsync(file.OpenReadStream(), "test.png", file.Length, "image/png");
        }

        return SendNoContentAsync();
    }
}
