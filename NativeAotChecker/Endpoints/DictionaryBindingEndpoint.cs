using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Dictionary binding in JSON request in AOT mode
public sealed class DictionaryBindingRequest
{
    public Dictionary<string, string> StringDict { get; set; } = new();
    public Dictionary<string, int> IntDict { get; set; } = new();
    public Dictionary<int, string> IntKeyDict { get; set; } = new();
    public Dictionary<string, List<string>> ListValueDict { get; set; } = new();
}

public sealed class DictionaryBindingResponse
{
    public int StringDictCount { get; set; }
    public int IntDictCount { get; set; }
    public int IntKeyDictCount { get; set; }
    public int ListValueDictCount { get; set; }
    public string? FirstStringValue { get; set; }
    public int? FirstIntValue { get; set; }
    public bool AllDictionariesBound { get; set; }
}

public sealed class DictionaryBindingEndpoint : Endpoint<DictionaryBindingRequest, DictionaryBindingResponse>
{
    public override void Configure()
    {
        Post("dictionary-binding-test");
        AllowAnonymous();
        SerializerContext<DictionaryBindingSerCtx>();
    }

    public override async Task HandleAsync(DictionaryBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new DictionaryBindingResponse
        {
            StringDictCount = req.StringDict.Count,
            IntDictCount = req.IntDict.Count,
            IntKeyDictCount = req.IntKeyDict.Count,
            ListValueDictCount = req.ListValueDict.Count,
            FirstStringValue = req.StringDict.Values.FirstOrDefault(),
            FirstIntValue = req.IntDict.Values.FirstOrDefault(),
            AllDictionariesBound = req.StringDict.Count > 0 || 
                                   req.IntDict.Count > 0 ||
                                   req.IntKeyDict.Count > 0 ||
                                   req.ListValueDict.Count > 0
        }, ct);
    }
}

[JsonSerializable(typeof(DictionaryBindingRequest))]
[JsonSerializable(typeof(DictionaryBindingResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, int>))]
[JsonSerializable(typeof(Dictionary<int, string>))]
[JsonSerializable(typeof(Dictionary<string, List<string>>))]
public partial class DictionaryBindingSerCtx : JsonSerializerContext;
