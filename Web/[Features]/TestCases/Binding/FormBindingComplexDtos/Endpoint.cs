namespace TestCases.FormBindingComplexDtos;

sealed class Request
{
    [FromForm]
    public Book Book { get; set; }
}

sealed class Book
{
    public string Title { get; set; }
    public IFormFile CoverImage { get; set; }
    public IFormFileCollection SourceFiles { get; set; }
    public Author MainAuthor { get; set; }
    public List<Author> CoAuthors { get; set; }
    public IEnumerable<int> BarCodes { get; set; }
}

sealed class Author
{
    public string Name { get; set; }
    public IFormFile ProfileImage { get; set; }
    public IFormFileCollection DocumentFiles { get; set; }
    public Address MainAddress { get; set; }
    public List<Address> OtherAddresses { get; set; }
}

sealed class Address
{
    public string Street { get; set; }
    public IFormFile MainImage { get; set; }
    public List<IFormFile> AlternativeImages { get; set; }
}

sealed class Endpoint : Endpoint<Request>
{
    internal static Book? Result { get; private set; }

    public override void Configure()
    {
        Post("test-cases/form-binding-complex-dtos");
        AllowAnonymous();
        AllowFileUploads();
    }

    public override Task HandleAsync(Request r, CancellationToken ct)
    {
        Result = r.Book;

        return Task.CompletedTask;
    }
}