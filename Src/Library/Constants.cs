namespace FastEndpoints;

[HideFromDocs]
public static class Constants
{
    //must match with FastEnpoints.Security.Constants.PermissionsClaimType
    //also the value should never change from "permissions" or third party auth providers won't work
    public const string PermissionsClaimType = "permissions";

    internal const string MisconfiguredMsg = "Please move UseFastEndpoints() above/before any terminating middleware such as UseEndpoints()";
}