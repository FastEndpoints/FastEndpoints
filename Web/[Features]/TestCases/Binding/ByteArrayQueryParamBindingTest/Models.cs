namespace TestCases.ByteArrayQueryParamBindingTest;

public class Request
{
    [QueryParam]
    public byte[] Timestamp { get; set; }

    [FromQueryParams]
    public ObjectWithByteArrays ObjectWithByteArrays { get; set; }
}

public class ObjectWithByteArrays
{
    public byte[] Timestamp { get; init; }
    public List<byte[]> Timestamps { get; init; }
}

public class Response
{
    public byte[] Timestamp { get; init; }
    public ObjectWithByteArrays ObjectWithByteArrays { get; init; }
}