using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Send redirect responses in AOT mode
public sealed class RedirectRequest
{
    [QueryParam]
    public string Target { get; set; } = "healthy";

    [QueryParam]
    public bool Permanent { get; set; } = false;
}

public sealed class RedirectEndpoint : Endpoint<RedirectRequest>
{
    public override void Configure()
    {
        Get("redirect-test");
        AllowAnonymous();
        SerializerContext<RedirectSerCtx>();
    }

    public override async Task HandleAsync(RedirectRequest req, CancellationToken ct)
    {
        if (req.Permanent)
        {
            await Send.RedirectAsync($"/{req.Target}", true);
        }
        else
        {
            await Send.RedirectAsync($"/{req.Target}");
        }
    }
}

// Test: Send.CreatedAtAsync response
public sealed class CreatedAtRequest
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

public sealed class CreatedAtResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class CreatedAtEndpoint : Endpoint<CreatedAtRequest, CreatedAtResponse>
{
    public override void Configure()
    {
        Post("created-at-test");
        AllowAnonymous();
        SerializerContext<RedirectSerCtx>();
    }

    public override async Task HandleAsync(CreatedAtRequest req, CancellationToken ct)
    {
        var response = new CreatedAtResponse
        {
            Id = Guid.NewGuid(),
            Name = req.Name,
            Value = req.Value,
            CreatedAt = DateTime.UtcNow
        };

        await Send.CreatedAtAsync<GetByIdEndpoint>(new { Id = response.Id }, response, cancellation: ct);
    }
}

// Helper endpoint for CreatedAt location
public sealed class GetByIdRequest
{
    public Guid Id { get; set; }
}

public sealed class GetByIdEndpoint : Endpoint<GetByIdRequest, CreatedAtResponse>
{
    public override void Configure()
    {
        Get("resource/{Id}");
        AllowAnonymous();
        SerializerContext<RedirectSerCtx>();
    }

    public override async Task HandleAsync(GetByIdRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new CreatedAtResponse
        {
            Id = req.Id,
            Name = "Retrieved",
            Value = "From GetById",
            CreatedAt = DateTime.UtcNow
        }, ct);
    }
}

[JsonSerializable(typeof(RedirectRequest))]
[JsonSerializable(typeof(CreatedAtRequest))]
[JsonSerializable(typeof(CreatedAtResponse))]
[JsonSerializable(typeof(GetByIdRequest))]
public partial class RedirectSerCtx : JsonSerializerContext;
