using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace NativeAotChecker.Endpoints;

// Test: Dependency Injection and Scoped Services in AOT mode

// A scoped service
public interface IScopedCounter
{
    int Increment();
    int GetValue();
}

public sealed class ScopedCounter : IScopedCounter
{
    private int _value;

    public int Increment() => ++_value;
    public int GetValue() => _value;
}

// A singleton service
public interface ISingletonService
{
    Guid GetInstanceId();
    int IncrementCallCount();
}

public sealed class SingletonService : ISingletonService
{
    private readonly Guid _instanceId = Guid.NewGuid();
    private int _callCount;

    public Guid GetInstanceId() => _instanceId;
    public int IncrementCallCount() => ++_callCount;
}

// A transient service
public interface ITransientService
{
    Guid GetInstanceId();
}

public sealed class TransientService : ITransientService
{
    private readonly Guid _instanceId = Guid.NewGuid();
    public Guid GetInstanceId() => _instanceId;
}

public sealed class DiTestResponse
{
    public int ScopedCounterValue { get; set; }
    public Guid SingletonInstanceId { get; set; }
    public int SingletonCallCount { get; set; }
    public Guid TransientInstanceId1 { get; set; }
    public Guid TransientInstanceId2 { get; set; }
    public bool TransientIdsAreDifferent { get; set; }
    public Guid ResolvedTransientId { get; set; }
}

public sealed class DiTestEndpoint : EndpointWithoutRequest<DiTestResponse>
{
    // Constructor injection
    public IScopedCounter ScopedCounter { get; set; } = null!;
    public ISingletonService SingletonService { get; set; } = null!;
    public ITransientService TransientService1 { get; set; } = null!;

    public override void Configure()
    {
        Get("di-test");
        AllowAnonymous();
        SerializerContext<DiTestSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // Use scoped counter
        ScopedCounter.Increment();
        ScopedCounter.Increment();
        ScopedCounter.Increment();

        // Get another transient via Resolve
        var transientService2 = Resolve<ITransientService>();

        await Send.OkAsync(new DiTestResponse
        {
            ScopedCounterValue = ScopedCounter.GetValue(),
            SingletonInstanceId = SingletonService.GetInstanceId(),
            SingletonCallCount = SingletonService.IncrementCallCount(),
            TransientInstanceId1 = TransientService1.GetInstanceId(),
            TransientInstanceId2 = transientService2.GetInstanceId(),
            TransientIdsAreDifferent = TransientService1.GetInstanceId() != transientService2.GetInstanceId(),
            ResolvedTransientId = transientService2.GetInstanceId()
        }, ct);
    }
}

[JsonSerializable(typeof(DiTestResponse))]
public partial class DiTestSerCtx : JsonSerializerContext;
