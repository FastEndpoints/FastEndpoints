using System.Text.RegularExpressions;

namespace FastEndpoints.OpenApi;

static partial class RouteTemplateHelpers
{
    public static string StripConstraints(string route)
    {
        if (!route.Contains('{'))
            return route;

        return RouteConstraintsRegex().Replace(route, "$1");
    }

    public static string NormalizePath(string route)
    {
        route = NormalizeRouteTemplate(route.TrimStart('~').TrimEnd('/'));

        return route.StartsWith('/') ? route : "/" + route;
    }

    public static string NormalizeRouteTemplate(string route)
        => ReplaceParameters(StripConstraints(route), NormalizeParameterName);

    public static List<string> GetParameterSegments(string? route)
    {
        var matches = RouteParamRegex().Matches(route ?? string.Empty);
        var segments = new List<string>(matches.Count);

        for (var i = 0; i < matches.Count; i++)
            segments.Add(matches[i].Groups[1].Value);

        return segments;
    }

    public static string ReplaceParameters(string route, Func<string, string> replacement)
        => RouteParamRegex().Replace(route, m => $"{{{replacement(m.Groups[1].Value)}}}");

    public static string NormalizeParameterName(string segment)
    {
        var colonIdx = segment.IndexOf(':');
        var equalsIdx = segment.IndexOf('=');
        var splitIdx = colonIdx >= 0 && equalsIdx >= 0
                           ? Math.Min(colonIdx, equalsIdx)
                           : Math.Max(colonIdx, equalsIdx);
        var name = splitIdx >= 0 ? segment[..splitIdx] : segment;

        return name.TrimStart('*').TrimEnd('?');
    }

    [GeneratedRegex(@"\{([^}]+)\}")]
    private static partial Regex RouteParamRegex();

    [GeneratedRegex("(?<={)\\**([^?:=}]+)[^}]*(?=})")]
    private static partial Regex RouteConstraintsRegex();
}
