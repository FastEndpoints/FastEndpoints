namespace FastEndpoints;

internal static class Constants
{
    //must match with FastEnpoints.Security.Constants.PermissionsClaimType
    //also the value should never change from "permissions" or third party auth providers won't work
    internal const string PermissionsClaimType = "permissions";

    //this is used as a dictionary key. int32.gethashcode() just returns the value
    internal const int ResponseSent = 0;
}