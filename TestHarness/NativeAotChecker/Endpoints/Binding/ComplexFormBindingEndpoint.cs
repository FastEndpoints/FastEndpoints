namespace NativeAotChecker.Endpoints.Binding;

public sealed class ComplexFormBindingRequest
{
    [FromForm]
    public FormBook Book { get; set; } = null!;
}

public sealed class FormBook
{
    public string Title { get; set; } = null!;
    public List<int> BarCodes { get; set; } = [];
    public FormAuthor Editor { get; set; } = null!;
    public List<FormAuthor> Authors { get; set; } = [];
    public IFormFile CoverImage { get; set; } = null!;
}

public sealed class FormAuthor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public sealed class ComplexFormBindingResponse
{
    public string Title { get; set; } = null!;
    public List<int> BarCodes { get; set; } = [];
    public FormAuthor Editor { get; set; } = null!;
    public List<FormAuthor> Authors { get; set; } = [];
    public string CoverImageFileName { get; set; } = null!;
}

public sealed class ComplexFormBindingEndpoint : Endpoint<ComplexFormBindingRequest, ComplexFormBindingResponse>
{
    public override void Configure()
    {
        Post("complex-form-binding");
        AllowAnonymous();
        AllowFileUploads();
    }

    public override async Task HandleAsync(ComplexFormBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Title = req.Book.Title,
                BarCodes = req.Book.BarCodes,
                Editor = req.Book.Editor,
                Authors = req.Book.Authors,
                CoverImageFileName = req.Book.CoverImage.FileName
            },
            ct);
    }
}
