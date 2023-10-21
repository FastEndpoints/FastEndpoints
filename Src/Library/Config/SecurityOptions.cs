namespace FastEndpoints;

/// <summary>
/// global security options
/// </summary>
public sealed class SecurityOptions
{
    /// <summary>
    /// specify a custom claim type used to identify permissions of a user principal. defaults to `permission`.
    /// <para>
    /// WARNING: do not change the default unless you fully comprehend what you're doing!!!
    /// </para>
    /// </summary>
    public string PermissionsClaimType { internal get; set; }
        = "permissions"; //should never change from "permissions" or third party auth providers such as Auth0 won't work.
}