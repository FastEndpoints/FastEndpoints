using Microsoft.AspNetCore.Authorization;

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
                        def.Roles(authAttr.Roles?.Split(','));
                        def.AuthSchemes(authAttr.AuthenticationSchemes?.Split(','));
                        if (authAttr.Policy is not null) def.Policies(new[] { authAttr.Policy });
                        break;

                    case ThrottleAttribute thrtAttr:
                        def.Throttle(thrtAttr.HitLimit, thrtAttr.DurationSeconds, thrtAttr.HeaderName);
                        break;
                }
            }
        }
    }
}
