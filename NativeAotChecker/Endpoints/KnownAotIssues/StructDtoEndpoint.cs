using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.KnownAotIssues;

// Test: Struct types as request/response DTOs in AOT mode
public readonly struct StructRequest
{
    public int Id { get; init; }
    public string Name { get; init; }
    public double Value { get; init; }
}

public readonly struct StructResponse
{
    public int Id { get; init; }
    public string Name { get; init; }
    public double Value { get; init; }
    public bool IsValid { get; init; }
}

public sealed class StructTypesEndpoint : Endpoint<StructRequest, StructResponse>
{
    public override void Configure()
    {
        Post("struct-types-test");
        AllowAnonymous();
        SerializerContext<StructTypesSerCtx>();
    }

    public override async Task HandleAsync(StructRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new StructResponse
        {
            Id = req.Id,
            Name = req.Name,
            Value = req.Value,
            IsValid = req.Id > 0 && !string.IsNullOrEmpty(req.Name)
        }, ct);
    }
}

[JsonSerializable(typeof(StructRequest))]
[JsonSerializable(typeof(StructResponse))]
public partial class StructTypesSerCtx : JsonSerializerContext;
