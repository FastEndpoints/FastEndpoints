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
            CustomName = req.Data.CustomName,
            FileName = req.Data.File.FileName,
            DocumentFileNames = req.Data.Documents.Select(f => f.FileName).ToArray(),
            DetailsTitle = req.Data.Details.Title,
            DetailsImageFileName = req.Data.Details.Image.FileName,
            GalleryFileNames = req.Data.Details.Gallery.Select(f => f.FileName).ToArray(),
            FirstItemDescription = req.Data.Items[0].Description,
            FirstItemAttachmentFileName = req.Data.Items[0].Attachment.FileName,
            NumbersSum = req.Data.Numbers.Sum()
        });
    }
}
