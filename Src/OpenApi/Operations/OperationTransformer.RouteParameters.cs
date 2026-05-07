using System.Text;

namespace FastEndpoints.OpenApi;

sealed partial class OperationTransformer
{
    static string BuildBareRoute(string documentPath, string? routePrefix, int endpointVersion)
    {
        var segments = documentPath.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();

        RemoveRoutePrefix(segments, routePrefix);
        RemoveVersionSegment(segments, endpointVersion);

        return "/" + string.Join('/', segments);
    }

    static void RemoveRoutePrefix(List<string> routeSegments, string? routePrefix)
    {
        if (string.IsNullOrWhiteSpace(routePrefix))
            return;

        var prefixSegments = routePrefix.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (!StartsWithSegments(routeSegments, prefixSegments))
            return;

        routeSegments.RemoveRange(0, prefixSegments.Length);
    }

    static bool StartsWithSegments(List<string> routeSegments, string[] prefixSegments)
    {
        if (routeSegments.Count < prefixSegments.Length)
            return false;

        for (var i = 0; i < prefixSegments.Length; i++)
        {
            if (!string.Equals(routeSegments[i], prefixSegments[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    static void RemoveVersionSegment(List<string> routeSegments, int endpointVersion)
    {
        if (endpointVersion <= 0)
            return;

        var versionSegment = $"{GlobalConfig.VersioningPrefix ?? "v"}{endpointVersion}";
        var versionIndex = routeSegments.IndexOf(versionSegment);

        if (versionIndex >= 0)
            routeSegments.RemoveAt(versionIndex);
    }

    internal static string? FindEndpointRouteTemplate(EndpointDefinition epDef, string documentPath)
    {
        if (epDef.Routes.Length == 0)
            return null;

        if (epDef.Routes.Length == 1)
            return epDef.Routes[0];

        foreach (var route in epDef.Routes)
        {
            var finalRoute = new StringBuilder().BuildRoute(epDef.Version.Current, route, epDef.OverriddenRoutePrefix);

            if (string.Equals(RouteTemplateHelpers.NormalizePath(finalRoute), documentPath, StringComparison.OrdinalIgnoreCase))
                return route;
        }

        return null;
    }

    internal static List<RouteParameterInfo> GetRouteParameters(string? relativePath)
    {
        var segments = RouteTemplateHelpers.GetParameterSegments(relativePath);
        var parameters = new List<RouteParameterInfo>(segments.Count);

        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            parameters.Add(
                new()
                {
                    Name = RouteTemplateHelpers.NormalizeParameterName(segment),
                    ConstraintType = segment.TryResolveRouteConstraintType()
                });
        }

        return parameters;
    }
}
