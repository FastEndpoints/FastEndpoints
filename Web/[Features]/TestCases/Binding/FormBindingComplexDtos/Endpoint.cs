namespace TestCases.FormBindingComplexDtos;

sealed class Book
{
    public string Title { get; set; }
    public IFormFile CoverImage { get; set; }
    public IFormFileCollection SourceFiles { get; set; }
    public Author MainAuthor { get; set; }
    public List<Author> CoAuthors { get; set; }
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
    public IFormFileCollection AlternativeImages { get; set; }
}

sealed class Endpoint : Endpoint<Book>
{

}