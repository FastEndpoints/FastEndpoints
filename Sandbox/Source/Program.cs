var bld = WebApplication.CreateBuilder(args);
bld.Services
   .SwaggerDocument()
   .AddFastEndpoints();

var app = bld.Build();
app.UseFastEndpoints()
   .UseSwaggerGen();
app.Run();

//public partial class Program;

sealed class SearchBookRequest
{
    [FromQuery]
    public Book Book { get; set; } // complex type to bind from query data
}

sealed class Book
{
    public string Title { get; set; }                // one primitive value
    public List<int> BarCodes { get; set; }          // multiple primitive values
    public Author Editor { get; set; }               // one complex value
    public IEnumerable<Author> Authors { get; set; } // multiple complex values
}

sealed class Author
{
    public Guid Id { get; set; }
    public string Name { get; set; }
}

sealed class MyEndpoint : Endpoint<SearchBookRequest>
{
    public override void Configure()
    {
        Get("book");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SearchBookRequest r, CancellationToken c)
    {
        await SendAsync(r);
    }
}