using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with JsonExtensionData
public class ExtensionDataRequest
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalData { get; set; }
}

// Response with JsonExtensionData
public class ExtensionDataResponse
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    public int AdditionalFieldCount { get; set; }
    public List<string> AdditionalKeys { get; set; } = [];
    public bool ExtensionDataWorked { get; set; }
    
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalData { get; set; }
}

/// <summary>
/// Tests [JsonExtensionData] attribute in AOT mode.
/// AOT ISSUE: JsonExtensionData uses reflection to find the extension property.
/// Dynamic property handling requires runtime type inspection.
/// Dictionary<string, object> deserialization needs runtime type resolution.
/// </summary>
public class ExtensionDataEndpoint : Endpoint<ExtensionDataRequest, ExtensionDataResponse>
{
    public override void Configure()
    {
        Post("extension-data-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExtensionDataRequest req, CancellationToken ct)
    {
        var additionalKeys = req.AdditionalData?.Keys.ToList() ?? [];

        await Send.OkAsync(new ExtensionDataResponse
        {
            Name = req.Name,
            Id = req.Id,
            AdditionalFieldCount = additionalKeys.Count,
            AdditionalKeys = additionalKeys,
            ExtensionDataWorked = additionalKeys.Count > 0,
            AdditionalData = req.AdditionalData
        });
    }
}
