using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Property injection vs constructor injection in AOT mode

// Service for property injection testing
public interface IPropertyInjectedService
{
    string GetValue();
    Guid GetId();
}

public sealed class PropertyInjectedService : IPropertyInjectedService
{
    private readonly Guid _id = Guid.NewGuid();
    public string GetValue() => "Property Injected!";
    public Guid GetId() => _id;
}

public sealed class PropertyInjectionResponse
{
    public string InjectedValue { get; set; } = string.Empty;
    public Guid InjectedServiceId { get; set; }
    public bool PropertyInjectionWorked { get; set; }
}

public sealed class PropertyInjectionEndpoint : EndpointWithoutRequest<PropertyInjectionResponse>
{
    // Property injection pattern
    public IPropertyInjectedService InjectedService { get; set; } = null!;

    public override void Configure()
    {
        Get("property-injection-test");
        AllowAnonymous();
        SerializerContext<PropertyInjectionSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new PropertyInjectionResponse
        {
            InjectedValue = InjectedService?.GetValue() ?? "NOT INJECTED",
            InjectedServiceId = InjectedService?.GetId() ?? Guid.Empty,
            PropertyInjectionWorked = InjectedService != null
        }, ct);
    }
}

// Test: Constructor injection
public sealed class ConstructorInjectionResponse
{
    public string InjectedValue { get; set; } = string.Empty;
    public Guid InjectedServiceId { get; set; }
    public bool ConstructorInjectionWorked { get; set; }
}

public sealed class ConstructorInjectionEndpoint : EndpointWithoutRequest<ConstructorInjectionResponse>
{
    private readonly IPropertyInjectedService _service;

    public ConstructorInjectionEndpoint(IPropertyInjectedService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("constructor-injection-test");
        AllowAnonymous();
        SerializerContext<PropertyInjectionSerCtx>();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await Send.OkAsync(new ConstructorInjectionResponse
        {
            InjectedValue = _service.GetValue(),
            InjectedServiceId = _service.GetId(),
            ConstructorInjectionWorked = true
        }, ct);
    }
}

[JsonSerializable(typeof(PropertyInjectionResponse))]
[JsonSerializable(typeof(ConstructorInjectionResponse))]
public partial class PropertyInjectionSerCtx : JsonSerializerContext;
