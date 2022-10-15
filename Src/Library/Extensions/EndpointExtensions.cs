using Microsoft.AspNetCore.Authorization;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints;

internal static class EndpointExtensions
{
    internal static string ActualName(this Type type)
        => (Nullable.GetUnderlyingType(type) ?? type).Name;

    internal static void Configure(this EndpointDefinition def, BaseEndpoint instance, bool implementsConfigure, object[] epAttribs)
    {
        if (implementsConfigure)
        {
            instance.Configure();
        }
        else
        {
            foreach (var att in epAttribs)
            {
                switch (att)
                {
                    case HttpAttribute httpAttr:
                        instance.Verbs(httpAttr.Verb);
                        def.Routes = httpAttr.Routes;
                        break;

                    case AllowAnonymousAttribute:
                        def.AllowAnonymous();
                        break;

                    case AuthorizeAttribute authAttr:
                        def.Roles(authAttr.Roles?.Split(',') ?? Array.Empty<string>());
                        def.AuthSchemes(authAttr.AuthenticationSchemes?.Split(',') ?? Array.Empty<string>());
                        if (authAttr.Policy is not null) def.Policies(new[] { authAttr.Policy });
                        break;

                    case ThrottleAttribute thrtAttr:
                        def.Throttle(thrtAttr.HitLimit, thrtAttr.DurationSeconds, thrtAttr.HeaderName);
                        break;
                }
            }
        }
    }

    private static readonly Regex rgx = new("(@[\\w]*)", RegexOptions.Compiled);
    internal static string BuildRoute<TRequest>(this Expression<Func<TRequest, object>> expr, string pattern) where TRequest : notnull
    {
        var sb = new StringBuilder(pattern);
        var matches = rgx.Matches(pattern);
        var i = 0;
        foreach (var prop in expr.PropNames())
        {
            if (i > matches.Count - 1)
                break;

            sb.Replace(matches[i].Value, prop);

            i++;
        }

        if (i == 0 || i != matches.Count)
            throw new ArgumentException($"Failed to build route: [{sb}] due to incorrect number of replacements!");

        return sb.ToString();
    }
}
