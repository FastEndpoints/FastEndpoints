using Asp.Versioning.Builder;

namespace FastEndpoints.AspVersioning;

/// <summary>
/// a container for globally holding the <see cref="ApiVersionSet"/> instances for the application
/// </summary>
public sealed class VersionSets : Dictionary<string, ApiVersionSet>
{
    internal static readonly VersionSets Container = new();
    internal static string VersionFormat = null!;

    /// <summary>
    /// creates a api/group/swagger-tag with an associated version set
    /// </summary>
    /// <param name="apiName">the name of the api (swagger tag)</param>
    /// <param name="builder">version set builder action</param>
    public static void CreateApi(string apiName, Action<ApiVersionSetBuilder> builder)
    {
        var setBuilder = new ApiVersionSetBuilder(apiName);
        builder(setBuilder);
        Container[apiName] = setBuilder.Build();
    }
}
