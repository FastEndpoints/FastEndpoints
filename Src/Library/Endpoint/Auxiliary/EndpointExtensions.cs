using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FastEndpoints;

static partial class EndpointExtensions
{
    /// <summary>
    /// determines if a given endpoint requires authorization.
    /// </summary>
    public static bool RequiresAuthorization(this EndpointDefinition ep)
        => ep.AllowedPermissions?.Count > 0 ||
           ep.AllowedScopes?.Count > 0 ||
           ep.AllowedClaimTypes?.Count > 0 ||
           ep.AllowedRoles?.Count > 0 ||
           ep.AuthSchemeNames?.Count > 0 ||
           ep.PolicyBuilder is not null ||
           ep.PreBuiltUserPolicies is not null;

    internal static string ActualTypeName(this Type type)
        => (Nullable.GetUnderlyingType(type) ?? type).Name;

    internal static void Initialize(this EndpointDefinition def, BaseEndpoint instance, HttpContext? ctx)
    {
        instance.Definition = def;
        instance.HttpContext = ctx!;

        if (def.ImplementsConfigure)
        {
            instance.Configure();

            if (instance.Definition is { FoundDuplicateValidators: true, ValidatorType: null })
            {
                throw new InvalidOperationException(
                    $"More than one validator was found for the request dto [{def.ReqDtoType.FullName}]. " +
                    "Specify the exact validator to register using the `Validator<TValidator>()` method in endpoint configuration.");
            }
        }
        else if (def.EndpointAttributes is not null)
        {
            foreach (var att in def.EndpointAttributes)
            {
                switch (att)
                {
                    case HttpAttribute httpAttr:
                        instance.Verbs(httpAttr.Verb.ToString("F"));
                        def.Routes = httpAttr.Routes;

                        break;

                    case AllowAnonymousAttribute:
                        def.AllowAnonymous();

                        break;

                    case AllowFileUploadsAttribute fileAttr:
                        def.AllowFileUploads(fileAttr.DontAutoBindFormData);

                        break;

                    case IAuthorizeData authAttr:
                        if (authAttr.Roles is not null)
                            def.Roles(authAttr.Roles.Split(','));

                        if (authAttr.AuthenticationSchemes is not null)
                            def.AuthSchemes(authAttr.AuthenticationSchemes.Split(','));

                        if (authAttr.Policy is not null)
                            def.Policies(authAttr.Policy);

                        break;

                    case ThrottleAttribute thrtAttr:
                        def.Throttle(thrtAttr.HitLimit, thrtAttr.DurationSeconds, thrtAttr.HeaderName);

                        break;

                    case IProcessorAttribute procAttr:
                        procAttr.AddToEndpointDefinition(def);

                        break;

                    case IGroupAttribute grpAttr:
                        grpAttr.InitGroup(def);

                        break;

                    case IApiResponseMetadataProvider prodAttr:
                        def.Options(b => b.Produces(prodAttr.StatusCode, prodAttr.Type));

                        break;

                    default:
                        def.AttribsToForward ??= [];
                        def.AttribsToForward.Add(att);

                        break;
                }
            }
        }
        def.Version.Init();
    }

    [GeneratedRegex("(@[\\w]*)")]
    private static partial Regex RouteBuilderRegex();

    internal static string BuildRoute<TRequest>(this Expression<Func<TRequest, object>> expr, string pattern) where TRequest : notnull
    {
        var sb = new StringBuilder(pattern);
        var matches = RouteBuilderRegex().Matches(pattern);
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
}