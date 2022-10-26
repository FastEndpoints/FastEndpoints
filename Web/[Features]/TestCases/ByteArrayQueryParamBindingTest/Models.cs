namespace TestCases.ByteArrayQueryParamBindingTest;


public class Request
{
    [QueryParam]
    public byte[] Timestamp { get; init; }
}

public class Response
{
    public byte[] Timestamp { get; init; }
}
