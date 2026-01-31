using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Record types as request/response DTOs in AOT mode
public sealed record RecordRequest(
    string Name,
    int Age,
    bool IsActive,
    DateTime CreatedAt
);

public sealed record RecordResponse(
    string Name,
    int Age,
    bool IsActive,
    DateTime CreatedAt,
    string ProcessedMessage
);

public sealed class RecordTypesEndpoint : Endpoint<RecordRequest, RecordResponse>
{
    public override void Configure()
    {
        Post("record-types-test");
        AllowAnonymous();
        SerializerContext<RecordTypesSerCtx>();
    }

    public override async Task HandleAsync(RecordRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new RecordResponse(
            req.Name,
            req.Age,
            req.IsActive,
            req.CreatedAt,
            $"Processed: {req.Name}, Age: {req.Age}"
        ), ct);
    }
}

// Test: Record with init properties
public sealed record InitPropertyRecord
{
    public string Id { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public int Number { get; init; }
}

public sealed class InitPropertyRecordEndpoint : Endpoint<InitPropertyRecord, InitPropertyRecord>
{
    public override void Configure()
    {
        Post("init-property-record-test");
        AllowAnonymous();
        SerializerContext<RecordTypesSerCtx>();
    }

    public override async Task HandleAsync(InitPropertyRecord req, CancellationToken ct)
    {
        await Send.OkAsync(req with { Value = $"Modified: {req.Value}" }, ct);
    }
}

[JsonSerializable(typeof(RecordRequest))]
[JsonSerializable(typeof(RecordResponse))]
[JsonSerializable(typeof(InitPropertyRecord))]
public partial class RecordTypesSerCtx : JsonSerializerContext;
