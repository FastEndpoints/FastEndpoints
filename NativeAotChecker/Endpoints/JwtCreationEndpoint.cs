using System.Text.Json.Serialization;
using FastEndpoints.Security;

namespace NativeAotChecker.Endpoints;

// Test: JWT token creation in AOT mode
public sealed class JwtCreateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
    public int ExpiryMinutes { get; set; } = 60;
}

public sealed class JwtCreateResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
}

public sealed class JwtCreationEndpoint : Endpoint<JwtCreateRequest, JwtCreateResponse>
{
    public override void Configure()
    {
        Post("jwt-creation");
        AllowAnonymous();
        SerializerContext<JwtCreateSerCtx>();
    }

    public override async Task HandleAsync(JwtCreateRequest req, CancellationToken ct)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(req.ExpiryMinutes);

        var token = JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = Config["Jwt-Secret"]!;
                o.ExpireAt = expiresAt;
                o.User.Claims.Add(("user-id", req.UserId));
                o.User.Claims.Add(("username", req.Username));

                foreach (var role in req.Roles)
                {
                    o.User.Roles.Add(role);
                }
            });

        await Send.OkAsync(new JwtCreateResponse
        {
            Token = token,
            ExpiresAt = expiresAt,
            UserId = req.UserId
        }, ct);
    }
}

// Test: Endpoint that requires JWT auth
public sealed class JwtProtectedRequest
{
    [FromClaim("user-id")]
    public string UserId { get; set; } = string.Empty;

    [FromClaim("username")]
    public string Username { get; set; } = string.Empty;
}

public sealed class JwtProtectedResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
}

public sealed class JwtProtectedEndpoint : Endpoint<JwtProtectedRequest, JwtProtectedResponse>
{
    public override void Configure()
    {
        Get("jwt-protected");
        // This endpoint requires authentication (no AllowAnonymous)
        SerializerContext<JwtCreateSerCtx>();
    }

    public override async Task HandleAsync(JwtProtectedRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JwtProtectedResponse
        {
            UserId = req.UserId,
            Username = req.Username,
            Message = $"Hello {req.Username}! Your claims were bound successfully.",
            IsAuthenticated = true
        }, ct);
    }
}

[JsonSerializable(typeof(JwtCreateRequest))]
[JsonSerializable(typeof(JwtCreateResponse))]
[JsonSerializable(typeof(JwtProtectedRequest))]
[JsonSerializable(typeof(JwtProtectedResponse))]
public partial class JwtCreateSerCtx : JsonSerializerContext;
