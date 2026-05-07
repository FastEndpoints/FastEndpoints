namespace TestCases.NestedFromFormBindingTest;

class Request
{
    [FromForm]
    public required FormData Data { get; set; }

    public class FormData
    {
        public required string Name { get; set; }
        public required IFormFile File { get; set; }
    }
}

sealed class Response
{
    public string Name { get; set; }
    public string FileName { get; set; }
}