namespace NativeAotChecker.Endpoints.Binding;

public class ComplexQueryBindingRequest
{
    [FromQuery]
    public Book Book { get; set; } = null!;
}

public class Book
{
    public string Title { get; set; } = null!;
    public List<int> BarCodes { get; set; } = [];
    public Author Editor { get; set; } = null!;
    public List<Author> Authors { get; set; } = [];
}

public class Author
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
}

public class ComplexQueryBindingEndpoint : Endpoint<ComplexQueryBindingRequest, Book>
{
    public override void Configure()
    {
        Get("complex-query-binding");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ComplexQueryBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(req.Book, ct);
    }
}