namespace FastEndpoints;

internal static class Constants
{
    //this is used as a dictionary key. int32.gethashcode() just returns the value
    internal const byte ResponseSent = 0;

    //must match Microsoft.AspNetCore.Http.ProducesResponseTypeMetadata sealed class
    internal const string ProducesMetadata = "ProducesResponseTypeMetadata";

    //must match Microsoft.AspNetCore.Http.Metadata.AcceptsMetadata sealed class
    internal const string AcceptsMetaData = "AcceptsMetadata";
}