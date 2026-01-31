using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Resolve<T> service locator pattern in AOT mode
public interface IAotTestService
{
    string GetMessage();
    Guid GetInstanceId();
}

public sealed class AotTestService : IAotTestService
{
    private readonly Guid _instanceId = Guid.NewGuid();
    
    public string GetMessage() => "Service resolved successfully!";
    public Guid GetInstanceId() => _instanceId;
}

public sealed class ServiceResolveResponse
{
    public string Message { get; set; } = string.Empty;
    public Guid ServiceInstanceId { get; set; }
    public bool ResolveWorked { get; set; }
}

public sealed class ServiceResolveEndpoint : EndpointWithoutRequest<ServiceResolveResponse>
{
    public override void Configure()
    {
        Get("service-resolve-test");
        AllowAnonymous();
        SerializerContext<ServiceResolveSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Test Resolve<T> pattern
        var service = Resolve<IAotTestService>();
        
        await Send.OkAsync(new ServiceResolveResponse
        {
            Message = service.GetMessage(),
            ServiceInstanceId = service.GetInstanceId(),
            ResolveWorked = true
        }, ct);
    }
}

// Test: TryResolve<T> pattern
public sealed class TryResolveResponse
{
    public bool ServiceFound { get; set; }
    public string? Message { get; set; }
}

public sealed class TryResolveEndpoint : EndpointWithoutRequest<TryResolveResponse>
{
    public override void Configure()
    {
        Get("try-resolve-test");
        AllowAnonymous();
        SerializerContext<ServiceResolveSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Test TryResolve<T> pattern
        var service = TryResolve<IAotTestService>();
        
        await Send.OkAsync(new TryResolveResponse
        {
            ServiceFound = service != null,
            Message = service?.GetMessage()
        }, ct);
    }
}

[JsonSerializable(typeof(ServiceResolveResponse))]
[JsonSerializable(typeof(TryResolveResponse))]
public partial class ServiceResolveSerCtx : JsonSerializerContext;
