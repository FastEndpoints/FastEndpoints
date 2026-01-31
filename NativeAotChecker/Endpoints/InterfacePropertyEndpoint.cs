namespace NativeAotChecker.Endpoints;

// Test interface properties in request/response - likely AOT issue
public interface IMetadata
{
    string Key { get; }
    string Value { get; }
}

public sealed class MetadataImpl : IMetadata
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class InterfacePropertyRequest
{
    public string Name { get; set; } = "";
    public IMetadata? Metadata { get; set; }
    public IList<string> Tags { get; set; } = new List<string>();
    public IEnumerable<int> Numbers { get; set; } = Enumerable.Empty<int>();
    public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();
}

public sealed class InterfacePropertyResponse
{
    public string Name { get; set; } = "";
    public bool HasMetadata { get; set; }
    public int TagCount { get; set; }
    public int NumberSum { get; set; }
    public int PropertyCount { get; set; }
}

public sealed class InterfacePropertyEndpoint : Endpoint<InterfacePropertyRequest, InterfacePropertyResponse>
{
    public override void Configure()
    {
        Post("interface-property-binding");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InterfacePropertyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new InterfacePropertyResponse
        {
            Name = req.Name,
            HasMetadata = req.Metadata is not null,
            TagCount = req.Tags.Count,
            NumberSum = req.Numbers.Sum(),
            PropertyCount = req.Properties.Count
        });
    }
}
