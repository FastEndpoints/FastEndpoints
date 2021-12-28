namespace FastEndpoints;

public class PlainTextRequest
{
    public string Content { get; init; }

    public PlainTextRequest(string content)
    {
        Content = content;
    }
}
