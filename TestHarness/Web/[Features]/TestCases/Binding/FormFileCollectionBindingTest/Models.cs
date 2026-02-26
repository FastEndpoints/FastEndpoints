namespace TestCases.FormFileBindingTest;

class Request
{
    public string ID { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public IFormFile File1 { get; set; }
    public IFormFile File2 { get; set; }

    public IEnumerable<IFormFile> Cars { get; set; }
    public IFormFileCollection Jets { get; set; }
}

sealed class Response
{
    public string File1Name { get; set; }
    public string File2Name { get; set; }
    public List<string> CarNames { get; set; }
    public List<string> JetNames { get; set; }
}