using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Sealed record class
public sealed record SealedRecordRequest(
    string Name,
    int Value,
    DateTime CreatedAt,
    List<string> Tags
);

public sealed record SealedRecordResponse(
    string Name,
    int Value,
    DateTime CreatedAt,
    int TagCount,
    string ProcessedMessage
);

/// <summary>
/// Tests sealed record types with primary constructor in AOT mode.
/// AOT ISSUE: Sealed record with primary constructor needs constructor parameter discovery.
/// Record deconstruction uses reflection for property mapping.
/// With expression (record with {...}) cloning uses reflection.
/// </summary>
public class SealedRecordEndpoint : Endpoint<SealedRecordRequest, SealedRecordResponse>
{
    public override void Configure()
    {
        Post("sealed-record-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(SealedRecordRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new SealedRecordResponse(
            Name: req.Name,
            Value: req.Value * 2,
            CreatedAt: req.CreatedAt,
            TagCount: req.Tags.Count,
            ProcessedMessage: $"Processed {req.Name} with value {req.Value}"
        ));
    }
}
