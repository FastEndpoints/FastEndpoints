using System.Security.Claims;

namespace FastEndpoints;

/// <summary>
/// the priviledges of the user which will be embedded in the jwt
/// </summary>
public sealed class UserPrivileges
{
    /// <summary>
    /// claims of the user
    /// </summary>
    public List<Claim> Claims { get; } = new();

    /// <summary>
    /// roles of the user
    /// </summary>
    public List<string> Roles { get; } = new();

    /// <summary>
    /// allowed permissions for the user
    /// </summary>
    public List<string> Permissions { get; } = new();
}