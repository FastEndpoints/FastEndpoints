using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Request with Lazy<T> properties
public class LazyLoadingRequest
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
}

// Response with Lazy<T> - problematic in AOT
public class LazyLoadingResponse
{
    public string Name { get; set; } = string.Empty;
    public int Id { get; set; }
    
    // Lazy<T> serialization is problematic
    [JsonIgnore]
    public Lazy<string> LazyComputed { get; set; } = new(() => "computed");
    
    // Expose the value instead
    public string ComputedValue => LazyComputed.Value;
    
    public bool LazyWorked { get; set; }
}

// Class with Func<T> properties
public class FuncPropertyRequest
{
    public string Input { get; set; } = string.Empty;
}

public class FuncPropertyResponse
{
    public string Input { get; set; } = string.Empty;
    
    [JsonIgnore]
    public Func<string, string> Transformer { get; set; } = s => s;
    
    public string TransformedValue { get; set; } = string.Empty;
    public bool FuncWorked { get; set; }
}

/// <summary>
/// Tests Lazy&lt;T&gt; property handling in AOT mode.
/// AOT ISSUE: Lazy&lt;T&gt; uses reflection for value factory invocation.
/// Generic type instantiation at runtime may fail.
/// ValueFactory delegate compilation needs JIT.
/// </summary>
public class LazyLoadingEndpoint : Endpoint<LazyLoadingRequest, LazyLoadingResponse>
{
    public override void Configure()
    {
        Get("lazy-loading-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LazyLoadingRequest req, CancellationToken ct)
    {
        var response = new LazyLoadingResponse
        {
            Name = req.Name,
            Id = req.Id,
            LazyComputed = new Lazy<string>(() => $"Computed for {req.Name} with ID {req.Id}"),
            LazyWorked = true
        };

        await Send.OkAsync(response);
    }
}

/// <summary>
/// Tests Func&lt;T&gt; properties in AOT mode.
/// AOT ISSUE: Func delegates need JIT compilation for invocation.
/// Delegate.DynamicInvoke uses reflection.
/// Lambda expression compilation is not AOT-friendly.
/// </summary>
public class FuncPropertyEndpoint : Endpoint<FuncPropertyRequest, FuncPropertyResponse>
{
    public override void Configure()
    {
        Post("func-property-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(FuncPropertyRequest req, CancellationToken ct)
    {
        Func<string, string> transformer = s => s.ToUpperInvariant();
        
        await Send.OkAsync(new FuncPropertyResponse
        {
            Input = req.Input,
            Transformer = transformer,
            TransformedValue = transformer(req.Input),
            FuncWorked = true
        });
    }
}
