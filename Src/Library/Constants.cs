namespace FastEndpoints;

[HideFromDocs]
public static class Constants
{
    //must match with FastEnpoints.Security.Constants.PermissionsClaimType
    //also the value should never change from "permissions" or third party auth providers won't work
    public const string PermissionsClaimType = "permissions";
}

