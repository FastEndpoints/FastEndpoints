using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Interface-based DTOs in AOT mode
public interface IDataContainer
{
    string Id { get; }
    string Data { get; }
}

public sealed class ConcreteDataContainer : IDataContainer
{
    public string Id { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
    public string ExtraInfo { get; set; } = string.Empty;
}

public sealed class InterfaceDtoRequest
{
    public string Name { get; set; } = string.Empty;
    public ConcreteDataContainer Container { get; set; } = new();
}

public sealed class InterfaceDtoResponse
{
    public string Name { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string ContainerData { get; set; } = string.Empty;
    public string ExtraInfo { get; set; } = string.Empty;
    public bool InterfaceWorked { get; set; }
}

public sealed class InterfaceDtoEndpoint : Endpoint<InterfaceDtoRequest, InterfaceDtoResponse>
{
    public override void Configure()
    {
        Post("interface-dto-test");
        AllowAnonymous();
        SerializerContext<InterfaceDtoSerCtx>();
    }

    public override async Task HandleAsync(InterfaceDtoRequest req, CancellationToken ct)
    {
        // Test that we can work with the interface
        IDataContainer container = req.Container;
        
        await Send.OkAsync(new InterfaceDtoResponse
        {
            Name = req.Name,
            ContainerId = container.Id,
            ContainerData = container.Data,
            ExtraInfo = req.Container.ExtraInfo,
            InterfaceWorked = !string.IsNullOrEmpty(container.Id) && !string.IsNullOrEmpty(container.Data)
        }, ct);
    }
}

[JsonSerializable(typeof(InterfaceDtoRequest))]
[JsonSerializable(typeof(InterfaceDtoResponse))]
[JsonSerializable(typeof(ConcreteDataContainer))]
public partial class InterfaceDtoSerCtx : JsonSerializerContext;
