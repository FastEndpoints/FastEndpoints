using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Class with private setters
public class PrivateSetterRequest
{
    [JsonInclude]
    public string Name { get; private set; } = string.Empty;
    
    [JsonInclude]
    public int Value { get; private set; }
    
    [JsonInclude]
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    
    // Public property for comparison
    public string PublicProperty { get; set; } = string.Empty;
}

// Class with internal setters
public class InternalSetterRequest
{
    [JsonInclude]
    public string Id { get; internal set; } = string.Empty;
    
    [JsonInclude]
    public decimal Amount { get; internal set; }
}

public class PrivateSetterResponse
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public DateTime CreatedAt { get; set; }
    public string PublicProperty { get; set; } = string.Empty;
    public string InternalId { get; set; } = string.Empty;
    public decimal InternalAmount { get; set; }
    public bool PrivateSetterWorked { get; set; }
    public bool InternalSetterWorked { get; set; }
}

/// <summary>
/// Tests [JsonInclude] with private/internal setters in AOT mode.
/// AOT ISSUE: Private setter access requires reflection with BindingFlags.NonPublic.
/// JsonInclude attribute discovery uses GetCustomAttribute.
/// Internal members need assembly-level visibility considerations.
/// </summary>
public class PrivateSetterEndpoint : Endpoint<PrivateSetterRequest, PrivateSetterResponse>
{
    public override void Configure()
    {
        Post("private-setter-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PrivateSetterRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new PrivateSetterResponse
        {
            Name = req.Name,
            Value = req.Value,
            CreatedAt = req.CreatedAt,
            PublicProperty = req.PublicProperty,
            PrivateSetterWorked = !string.IsNullOrEmpty(req.Name) && req.Value > 0
        });
    }
}

public class InternalSetterEndpoint : Endpoint<InternalSetterRequest, PrivateSetterResponse>
{
    public override void Configure()
    {
        Post("internal-setter-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(InternalSetterRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new PrivateSetterResponse
        {
            InternalId = req.Id,
            InternalAmount = req.Amount,
            InternalSetterWorked = !string.IsNullOrEmpty(req.Id) && req.Amount > 0
        });
    }
}
