namespace NativeAotChecker.Endpoints;

// Test record types with init-only setters - likely AOT issue
public sealed record RecordWithInitRequest
{
    public required string Name { get; init; }
    public required int Age { get; init; }
    public string? OptionalField { get; init; }
}

public sealed record RecordWithInitResponse
{
    public required string Greeting { get; init; }
    public required bool HasOptional { get; init; }
}

public sealed class RecordWithInitEndpoint : Endpoint<RecordWithInitRequest, RecordWithInitResponse>
{
    public override void Configure()
    {
        Post("record-with-init");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RecordWithInitRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new RecordWithInitResponse
        {
            Greeting = $"Hello {req.Name}, you are {req.Age} years old",
            HasOptional = req.OptionalField is not null
        });
    }
}
