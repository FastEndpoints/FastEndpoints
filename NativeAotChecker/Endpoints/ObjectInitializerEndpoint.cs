using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Class with init-only properties (C# 9+)
public class InitOnlyRequest
{
    public string Name { get; init; } = string.Empty;
    public int Value { get; init; }
    public List<string> Items { get; init; } = [];
    public NestedInitOnly Nested { get; init; } = new();
}

public class NestedInitOnly
{
    public string Description { get; init; } = string.Empty;
    public double Score { get; init; }
}

// Class with required init properties (C# 11+)
public class RequiredInitRequest
{
    public required string RequiredName { get; init; }
    public required int RequiredValue { get; init; }
    public string? OptionalField { get; init; }
}

public class InitOnlyResponse
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public int ItemCount { get; set; }
    public string NestedDescription { get; set; } = string.Empty;
    public double NestedScore { get; set; }
    public bool InitOnlyWorked { get; set; }
}

public class RequiredInitResponse
{
    public string RequiredName { get; set; } = string.Empty;
    public int RequiredValue { get; set; }
    public string? OptionalField { get; set; }
    public bool RequiredInitWorked { get; set; }
}

/// <summary>
/// Tests init-only properties in AOT mode.
/// AOT ISSUE: Init-only setters need special handling during deserialization.
/// Compiler generates modreq for init setters which needs metadata preservation.
/// Property setter invocation for init properties uses reflection.
/// </summary>
public class InitOnlyEndpoint : Endpoint<InitOnlyRequest, InitOnlyResponse>
{
    public override void Configure()
    {
        Post("init-only-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InitOnlyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new InitOnlyResponse
        {
            Name = req.Name,
            Value = req.Value,
            ItemCount = req.Items.Count,
            NestedDescription = req.Nested.Description,
            NestedScore = req.Nested.Score,
            InitOnlyWorked = !string.IsNullOrEmpty(req.Name) && req.Value > 0
        });
    }
}

/// <summary>
/// Tests required init properties in AOT mode.
/// AOT ISSUE: Required modifier needs SetsRequiredMembersAttribute handling.
/// Constructor analysis for required members uses reflection.
/// Validation of required properties at runtime needs metadata.
/// </summary>
public class RequiredInitEndpoint : Endpoint<RequiredInitRequest, RequiredInitResponse>
{
    public override void Configure()
    {
        Post("required-init-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RequiredInitRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new RequiredInitResponse
        {
            RequiredName = req.RequiredName,
            RequiredValue = req.RequiredValue,
            OptionalField = req.OptionalField,
            RequiredInitWorked = !string.IsNullOrEmpty(req.RequiredName) && req.RequiredValue > 0
        });
    }
}
