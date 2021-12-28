namespace FastEndpoints;

public interface IPlainTextRequest
{
    string Content { get; set; }
}

public class PlainTextRequest : IPlainTextRequest
{
    public string Content { get; set; }
}
