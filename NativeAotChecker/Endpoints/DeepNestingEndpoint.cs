namespace NativeAotChecker.Endpoints;

// Test deeply nested object binding - likely AOT issue
public sealed class NestLevel3
{
    public string DeepValue { get; set; } = "";
    public int DeepNumber { get; set; }
}

public sealed class NestLevel2
{
    public string MidValue { get; set; } = "";
    public NestLevel3 Level3 { get; set; } = new();
    public List<NestLevel3> Level3List { get; set; } = new();
}

public sealed class NestLevel1
{
    public string TopValue { get; set; } = "";
    public NestLevel2 Level2 { get; set; } = new();
    public Dictionary<string, NestLevel2> Level2Dict { get; set; } = new();
}

public sealed class DeepNestingRequest
{
    public NestLevel1 Root { get; set; } = new();
    public List<NestLevel1> RootList { get; set; } = new();
}

public sealed class DeepNestingResponse
{
    public string DeepestValue { get; set; } = "";
    public int TotalLevel3Count { get; set; }
    public List<string> AllDeepValues { get; set; } = new();
}

public sealed class DeepNestingEndpoint : Endpoint<DeepNestingRequest, DeepNestingResponse>
{
    public override void Configure()
    {
        Post("deep-nesting-binding");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DeepNestingRequest req, CancellationToken ct)
    {
        var allDeepValues = new List<string>();
        var totalCount = 0;

        // Collect from root
        allDeepValues.Add(req.Root.Level2.Level3.DeepValue);
        totalCount += 1 + req.Root.Level2.Level3List.Count;
        allDeepValues.AddRange(req.Root.Level2.Level3List.Select(l => l.DeepValue));

        // Collect from dictionary
        foreach (var kvp in req.Root.Level2Dict)
        {
            allDeepValues.Add(kvp.Value.Level3.DeepValue);
            totalCount += 1 + kvp.Value.Level3List.Count;
        }

        // Collect from list
        foreach (var item in req.RootList)
        {
            allDeepValues.Add(item.Level2.Level3.DeepValue);
            totalCount += 1 + item.Level2.Level3List.Count;
        }

        await Send.OkAsync(new DeepNestingResponse
        {
            DeepestValue = req.Root.Level2.Level3.DeepValue,
            TotalLevel3Count = totalCount,
            AllDeepValues = allDeepValues.Where(v => !string.IsNullOrEmpty(v)).ToList()
        });
    }
}
