using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request for anonymous type test
public class AnonymousTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> Tags { get; set; } = [];
}

/// <summary>
/// Tests anonymous type serialization in AOT mode.
/// AOT ISSUE: Anonymous types are compiler-generated and not registered with source generator.
/// typeof(anonymousType) at runtime fails without metadata.
/// Anonymous type property access uses dynamic compilation.
/// </summary>
public class AnonymousTypeEndpoint : Endpoint<AnonymousTypeRequest>
{
    public override void Configure()
    {
        Post("anonymous-type-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AnonymousTypeRequest req, CancellationToken ct)
    {
        // Return anonymous type - this is problematic in AOT
        var response = new
        {
            Name = req.Name,
            Count = req.Count,
            Tags = req.Tags,
            Computed = $"{req.Name} has {req.Count} items",
            Timestamp = DateTime.UtcNow,
            AnonymousTypeWorked = true
        };

        await HttpContext.Response.WriteAsJsonAsync(response, ct);
    }
}

/// <summary>
/// Tests nested anonymous types in AOT mode.
/// </summary>
public class NestedAnonymousEndpoint : Endpoint<AnonymousTypeRequest>
{
    public override void Configure()
    {
        Post("nested-anonymous-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(AnonymousTypeRequest req, CancellationToken ct)
    {
        // Nested anonymous types are even more problematic
        var response = new
        {
            Outer = new
            {
                Name = req.Name,
                Inner = new
                {
                    Count = req.Count,
                    DeepInner = new
                    {
                        Tags = req.Tags,
                        Timestamp = DateTime.UtcNow
                    }
                }
            },
            Success = true
        };

        await HttpContext.Response.WriteAsJsonAsync(response, ct);
    }
}
