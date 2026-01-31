using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request/Response DTOs
public class ThrottleRequest
{
    public string ClientId { get; set; } = string.Empty;
    public int RequestNumber { get; set; }
}

public class ThrottleResponse
{
    public string ClientId { get; set; } = string.Empty;
    public int RequestNumber { get; set; }
    public bool ThrottleConfigured { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Tests Throttle() rate limiting in AOT mode.
/// AOT ISSUE: Throttle uses header-based client identification which may use reflection.
/// Rate limit tracking uses concurrent dictionaries with runtime type handling.
/// </summary>
public class ThrottleTestEndpoint : Endpoint<ThrottleRequest, ThrottleResponse>
{
    public override void Configure()
    {
        Post("throttle-test");
        AllowAnonymous();
        Throttle(hitLimit: 5, durationSeconds: 60, headerName: "X-Client-Id");
    }

    public override async Task HandleAsync(ThrottleRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ThrottleResponse
        {
            ClientId = req.ClientId,
            RequestNumber = req.RequestNumber,
            ThrottleConfigured = true,
            Message = $"Request {req.RequestNumber} from client {req.ClientId}"
        });
    }
}
