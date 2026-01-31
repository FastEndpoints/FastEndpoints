using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request/Response DTOs
public class IdempotencyRequest
{
    public string OperationId { get; set; } = string.Empty;
    public string Data { get; set; } = string.Empty;
}

public class IdempotencyResponse
{
    public string OperationId { get; set; } = string.Empty;
    public string ProcessedData { get; set; } = string.Empty;
    public bool IdempotencyConfigured { get; set; }
    public DateTime ProcessedAt { get; set; }
}

/// <summary>
/// Tests Idempotency() configuration in AOT mode.
/// AOT ISSUE: Idempotency uses header-based request identification.
/// Cached response storage and retrieval may use reflection for serialization.
/// IdempotencyOptions configuration uses reflection-based property setting.
/// </summary>
public class IdempotencyTestEndpoint : Endpoint<IdempotencyRequest, IdempotencyResponse>
{
    public override void Configure()
    {
        Post("idempotency-test");
        AllowAnonymous();
        Idempotency(o =>
        {
            o.HeaderName = "X-Idempotency-Key";
        });
    }

    public override async Task HandleAsync(IdempotencyRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new IdempotencyResponse
        {
            OperationId = req.OperationId,
            ProcessedData = $"Processed: {req.Data}",
            IdempotencyConfigured = true,
            ProcessedAt = DateTime.UtcNow
        });
    }
}
