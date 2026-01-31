namespace NativeAotChecker.Endpoints;

// Test complex dictionary binding in request - likely AOT issue
public sealed class DictionaryComplexRequest
{
    public Dictionary<string, List<int>> TagScores { get; set; } = new();
    public Dictionary<int, string> IdNames { get; set; } = new();
    public Dictionary<string, NestedDictValue> Metadata { get; set; } = new();
}

public sealed class NestedDictValue
{
    public string Value { get; set; } = "";
    public int Priority { get; set; }
}

public sealed class DictionaryComplexResponse
{
    public int TagScoresCount { get; set; }
    public int IdNamesCount { get; set; }
    public int MetadataCount { get; set; }
    public List<string> AllKeys { get; set; } = new();
}

public sealed class DictionaryComplexBindingEndpoint : Endpoint<DictionaryComplexRequest, DictionaryComplexResponse>
{
    public override void Configure()
    {
        Post("dictionary-complex-binding");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DictionaryComplexRequest req, CancellationToken ct)
    {
        var allKeys = new List<string>();
        allKeys.AddRange(req.TagScores.Keys);
        allKeys.AddRange(req.IdNames.Keys.Select(k => k.ToString()));
        allKeys.AddRange(req.Metadata.Keys);

        await Send.OkAsync(new DictionaryComplexResponse
        {
            TagScoresCount = req.TagScores.Count,
            IdNamesCount = req.IdNames.Count,
            MetadataCount = req.Metadata.Count,
            AllKeys = allKeys
        });
    }
}
