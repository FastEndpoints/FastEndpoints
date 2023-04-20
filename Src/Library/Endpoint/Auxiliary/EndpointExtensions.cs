using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using static FastEndpoints.Config;

namespace FastEndpoints;

internal static class EndpointExtensions
{
    internal static string ActualTypeName(this Type type)
        => (Nullable.GetUnderlyingType(type) ?? type).Name;

    internal static bool RequiresAuthorization(this EndpointDefinition ep)
    {
        return ep.AllowedPermissions?.Any() is true ||
               ep.AllowedClaimTypes?.Any() is true ||
               ep.AllowedRoles?.Any() is true ||
               ep.AuthSchemeNames?.Any() is true ||
               ep.PolicyBuilder is not null;
    }

    internal static void Initialize(this EndpointDefinition def, BaseEndpoint instance, HttpContext? ctx)
    {
        instance.Definition = def;
        instance.HttpContext = ctx!;

        if (def.ImplementsConfigure)
        {
            instance.Configure();
            if (instance.Definition.FoundDuplicateValidators && instance.Definition.ValidatorType is null)
            {
                throw new InvalidOperationException(
                    $"More than one validator was found for the request dto [{def.ReqDtoType.FullName}]. " +
                    "Specify the exact validator to register using the `Validator<TValidator>()` method in endpoint configuration.");
            }
        }
        else if (def.EpAttributes is not null)
        {
            foreach (var att in def.EpAttributes)
            {
                switch (att)
                {
                    case HttpAttribute httpAttr:
                        instance.Verbs(httpAttr.Verb.ToString());
                        def.Routes = httpAttr.Routes;
                        break;

                    case AllowAnonymousAttribute:
                        def.AllowAnonymous();
                        break;

                    case AuthorizeAttribute authAttr:
                        if (authAttr.Roles is not null)
                            def.Roles(authAttr.Roles.Split(','));

                        if (authAttr.AuthenticationSchemes is not null)
                            def.AuthSchemes(authAttr.AuthenticationSchemes.Split(','));

                        if (authAttr.Policy is not null)
                            def.Policies(new[] { authAttr.Policy });

                        break;

                    case ThrottleAttribute thrtAttr:
                        def.Throttle(thrtAttr.HitLimit, thrtAttr.DurationSeconds, thrtAttr.HeaderName);
                        break;
                }
            }
        }
        def.IsInitialized = true;
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

        return i == 0 || i != matches.Count
                ? throw new ArgumentException($"Failed to build route: [{sb}] due to incorrect number of replacements!")
                : sb.ToString();
    }

    internal static string EndpointName(this Type epType, string? verb = null, int? routeNum = null)
    {
        var vrb = verb != null ? verb[0] + verb[1..].ToLowerInvariant() : null;
        var ep = EpOpts.ShortNames ? epType.Name : epType.FullName!.Replace(".", string.Empty);
        return vrb + ep + routeNum.ToString();
    }
}
