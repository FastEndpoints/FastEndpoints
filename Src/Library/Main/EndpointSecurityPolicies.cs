using System.Collections.Frozen;
using Microsoft.AspNetCore.Authorization;

namespace FastEndpoints;

static class EndpointSecurityPolicies
{
    internal static IAuthorizeData[] BuildAuthorizeAttributes(EndpointDefinition ep)
    {
        var policiesToAdd = new List<string>();

        if (ep.PreBuiltUserPolicies?.Count > 0)
            policiesToAdd.AddRange(ep.PreBuiltUserPolicies);

        if (ep.RequiresAuthorization())
            policiesToAdd.Add(ep.SecurityPolicyName);

        // ReSharper disable once CoVariantArrayConversion
        return policiesToAdd.Select(
            p =>
            {
                var attr = new AuthorizeAttribute { Policy = p };

                if (ep.AuthSchemeNames is not null)
                    attr.AuthenticationSchemes = string.Join(',', ep.AuthSchemeNames);

                if (ep.AllowedRoles is not null)
                    attr.Roles = string.Join(',', ep.AllowedRoles);

                return attr;
            }).ToArray();
    }

    internal static void AddSecurityPolicy(AuthorizationOptions opts, EndpointDefinition ep)
    {
        if (!ep.RequiresAuthorization())
            return;

        opts.AddPolicy(
            ep.SecurityPolicyName,
            b =>
            {
                b.RequireAuthenticatedUser();

                if (ep.AllowedPermissions?.Count > 0)
                {
                    if (ep.AllowAnyPermission)
                    {
                        var allowedPermissions = ep.AllowedPermissions.ToFrozenSet(StringComparer.Ordinal);
                        b.RequireAssertion(
                            x => x.User.Claims.Any(
                                c => string.Equals(c.Type, Cfg.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                     allowedPermissions.Contains(c.Value)));
                    }
                    else
                    {
                        b.RequireAssertion(
                            x => ep.AllowedPermissions.All(
                                p => x.User.Claims.Any(
                                    c => string.Equals(c.Type, Cfg.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                                         string.Equals(c.Value, p, StringComparison.Ordinal))));
                    }
                }

                if (ep.AllowedScopes?.Count > 0)
                {
                    if (ep.AllowAnyScope)
                    {
                        var allowedScopes = ep.AllowedScopes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
                        b.RequireAssertion(
                            x => x.User.Claims.Any(
                                c => string.Equals(c.Type, Cfg.SecOpts.ScopeClaimType, StringComparison.OrdinalIgnoreCase) &&
                                     Cfg.SecOpts.ScopeParser(c.Value).Any(allowedScopes.Contains)));
                    }
                    else
                    {
                        b.RequireAssertion(
                            x => x.User.Claims.Any(
                                c =>
                                {
                                    var incomingScopes = Cfg.SecOpts.ScopeParser(c.Value); //run parser func only once!

                                    return string.Equals(c.Type, Cfg.SecOpts.ScopeClaimType, StringComparison.OrdinalIgnoreCase) &&
                                           ep.AllowedScopes.All(s => incomingScopes.Contains(s, StringComparer.OrdinalIgnoreCase));
                                }));
                    }
                }

                if (ep.AllowedClaimTypes?.Count > 0)
                {
                    if (ep.AllowAnyClaim)
                    {
                        var allowedClaimTypes = ep.AllowedClaimTypes.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
                        b.RequireAssertion(x => x.User.Claims.Any(c => allowedClaimTypes.Contains(c.Type)));
                    }
                    else
                        b.RequireAssertion(x => ep.AllowedClaimTypes.All(t => x.User.Claims.Any(c => string.Equals(c.Type, t, StringComparison.OrdinalIgnoreCase))));
                }

                ep.PolicyBuilder?.Invoke(b);

                //note: only claim/permission/scope/policy-builder requirements are added here in the security policy
                //      roles and auth schemes are specified in the AuthorizeAttribute in BuildAuthorizeAttributes()
            });
    }
}
