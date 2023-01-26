using System.Security.Claims;

namespace FastEndpoints;

/// <summary>
/// the priviledges of the user which will be embedded in the jwt or cookie
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

    /// <summary>
    /// shortcut for adding a new <see cref="Claim"/> to the claim list for the given claim type and value
    /// </summary>
    /// <param name="claimType">the claim type to add</param>
    public string this[string claimType] {
        set => Claims.Add(new Claim(claimType, value));
    }
}