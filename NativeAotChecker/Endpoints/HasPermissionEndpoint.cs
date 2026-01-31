using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: HasPermission attribute binding in AOT mode
public sealed class PermissionCheckRequest
{
    [FromClaim("user-id")]
    public string UserId { get; set; } = string.Empty;

    [HasPermission("admin:read", IsRequired = false)]
    public bool HasAdminRead { get; set; }

    [HasPermission("admin:write", IsRequired = false)]
    public bool HasAdminWrite { get; set; }

    [HasPermission("user:read", IsRequired = false)]
    public bool HasUserRead { get; set; }
}

public sealed class PermissionCheckResponse
{
    public string UserId { get; set; } = string.Empty;
    public bool HasAdminRead { get; set; }
    public bool HasAdminWrite { get; set; }
    public bool HasUserRead { get; set; }
    public int PermissionCount { get; set; }
}

public sealed class HasPermissionEndpoint : Endpoint<PermissionCheckRequest, PermissionCheckResponse>
{
    public override void Configure()
    {
        Get("has-permission-test");
        // Requires auth to check permissions
        SerializerContext<PermissionCheckSerCtx>();
    }

    public override async Task HandleAsync(PermissionCheckRequest req, CancellationToken ct)
    {
        var permCount = 0;
        if (req.HasAdminRead) permCount++;
        if (req.HasAdminWrite) permCount++;
        if (req.HasUserRead) permCount++;

        await Send.OkAsync(new PermissionCheckResponse
        {
            UserId = req.UserId,
            HasAdminRead = req.HasAdminRead,
            HasAdminWrite = req.HasAdminWrite,
            HasUserRead = req.HasUserRead,
            PermissionCount = permCount
        }, ct);
    }
}

[JsonSerializable(typeof(PermissionCheckRequest))]
[JsonSerializable(typeof(PermissionCheckResponse))]
public partial class PermissionCheckSerCtx : JsonSerializerContext;
