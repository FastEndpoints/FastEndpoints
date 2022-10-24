using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints;

internal static class EndpointExtensions
{
    internal static string ActualName(this Type type)
        => (Nullable.GetUnderlyingType(type) ?? type).Name;

    internal static void Initialize(this EndpointDefinition def, BaseEndpoint instance, HttpContext? ctx)
    {
        instance.Definition = def;
        instance.HttpContext = ctx!;

        if (def.ImplementsConfigure)
        {
            instance.Configure();
        }
        else
        {
            if (def.EpAttributes is not null)
            {
                foreach (var att in def.EpAttributes)
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

        if (ctx?.RequestServices is not null)
        {
            if (def.ValidatorInstance is null && def.ValidatorType is not null)
                def.ValidatorInstance = ActivatorUtilities.CreateInstance(ctx.RequestServices, def.ValidatorType);
            if (def.MapperInstance is null && def.MapperType is not null)
                def.MapperInstance = ActivatorUtilities.CreateInstance(ctx.RequestServices, def.MapperType);
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
