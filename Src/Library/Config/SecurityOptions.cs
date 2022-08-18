namespace FastEndpoints;

/// <summary>
/// global security options
/// </summary>
public class SecurityOptions
{
    /// <summary>
    /// specify a custom claim type used to identify permissions of a user principal. defaults to `permission`.
    /// <para>WARNING: the <c>FastEndpoints.Security</c> package should not be used if you're setting a custom permission claim type here.
    /// do not change the default unless you fully comprehend what you're doing!!!
    /// </para>
    /// </summary>
    public string PermissionsClaimType { internal get; set; }
        = "permissions";
    //the default value must match with FastEnpoints.Security.Constants.PermissionsClaimType.
    //should never change from "permissions" or third party auth providers such as Auth0 won't work.
}