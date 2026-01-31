using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Inherited endpoint classes in AOT mode
public abstract class BaseRequest
{
    public string BaseProperty { get; set; } = string.Empty;
    public int BaseId { get; set; }
}

public sealed class InheritedRequest : BaseRequest
{
    public string ChildProperty { get; set; } = string.Empty;
    public bool ChildFlag { get; set; }
}

public abstract class BaseResponse
{
    public string BaseMessage { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public sealed class InheritedResponse : BaseResponse
{
    public string ChildMessage { get; set; } = string.Empty;
    public bool Success { get; set; }
}

public sealed class InheritedDtoEndpoint : Endpoint<InheritedRequest, InheritedResponse>
{
    public override void Configure()
    {
        Post("inherited-dto-test");
        AllowAnonymous();
        SerializerContext<InheritedDtoSerCtx>();
    }

    public override async Task HandleAsync(InheritedRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new InheritedResponse
        {
            BaseMessage = $"Base: {req.BaseProperty}",
            Timestamp = DateTime.UtcNow,
            ChildMessage = $"Child: {req.ChildProperty}",
            Success = req.BaseId > 0 && req.ChildFlag
        }, ct);
    }
}

// Test: Generic base endpoint
public abstract class GenericBaseEndpoint<TRequest, TResponse> : Endpoint<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : notnull
{
    protected string GetTypeName() => typeof(TRequest).Name;
}

public sealed class GenericInheritedRequest
{
    public string Data { get; set; } = string.Empty;
}

public sealed class GenericInheritedResponse
{
    public string Data { get; set; } = string.Empty;
    public string RequestTypeName { get; set; } = string.Empty;
}

public sealed class GenericInheritedEndpoint : GenericBaseEndpoint<GenericInheritedRequest, GenericInheritedResponse>
{
    public override void Configure()
    {
        Post("generic-inherited-endpoint-test");
        AllowAnonymous();
        SerializerContext<InheritedDtoSerCtx>();
    }

    public override async Task HandleAsync(GenericInheritedRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new GenericInheritedResponse
        {
            Data = req.Data,
            RequestTypeName = GetTypeName()
        }, ct);
    }
}

[JsonSerializable(typeof(InheritedRequest))]
[JsonSerializable(typeof(InheritedResponse))]
[JsonSerializable(typeof(GenericInheritedRequest))]
[JsonSerializable(typeof(GenericInheritedResponse))]
public partial class InheritedDtoSerCtx : JsonSerializerContext;
