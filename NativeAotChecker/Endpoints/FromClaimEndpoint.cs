using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request with [FromClaim] attribute binding
public class FromClaimRequest
{
    /// <summary>
    /// Binds from 'sub' claim automatically
    /// </summary>
    [FromClaim(ClaimType = "sub", IsRequired = false)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Binds from 'email' claim
    /// </summary>
    [FromClaim(ClaimType = "email", IsRequired = false)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Binds from 'role' claim
    /// </summary>
    [FromClaim(ClaimType = "role", IsRequired = false)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Regular property not from claim
    /// </summary>
    public string AdditionalData { get; set; } = string.Empty;
}

public class FromClaimResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AdditionalData { get; set; } = string.Empty;
    public bool HasClaims { get; set; }
    public bool FromClaimWorked { get; set; }
}

/// <summary>
/// Tests [FromClaim] attribute binding in AOT mode.
/// AOT ISSUE: Attribute discovery uses reflection at startup.
/// Claim type extraction uses string-based property access.
/// Binding logic inspects property attributes via reflection.
/// </summary>
public class FromClaimEndpoint : Endpoint<FromClaimRequest, FromClaimResponse>
{
    public override void Configure()
    {
        Post("from-claim-test");
        AllowAnonymous(); // Claims will be empty for anonymous
    }

    public override async Task HandleAsync(FromClaimRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new FromClaimResponse
        {
            UserId = req.UserId,
            Email = req.Email,
            Role = req.Role,
            AdditionalData = req.AdditionalData,
            HasClaims = !string.IsNullOrEmpty(req.UserId) || !string.IsNullOrEmpty(req.Email),
            FromClaimWorked = true
        });
    }
}
