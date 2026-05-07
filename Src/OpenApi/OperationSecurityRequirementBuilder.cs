using Microsoft.AspNetCore.Authorization;

namespace FastEndpoints.OpenApi;

sealed class OperationSecurityRequirementBuilder(DocumentOptions docOpts)
{
    internal (string SchemeName, string[] Scopes)[] Build(EndpointDefinition? epDef, IEnumerable<AuthorizeAttribute> authorizeAttributes)
    {
        var scopes = BuildScopes(authorizeAttributes);
        var securityEntries = new List<(string SchemeName, string[] Scopes)>();

        foreach (var authConfig in docOpts.AuthSchemes)
        {
            if (epDef is not null)
            {
                var epSchemes = epDef.AuthSchemeNames;

                if (epSchemes?.Contains(authConfig.Name) == false)
                    continue;
            }

            var mergedScopes = new HashSet<string>(scopes, StringComparer.Ordinal);

            if (authConfig.GlobalScopes is not null)
            {
                foreach (var scope in authConfig.GlobalScopes)
                    mergedScopes.Add(scope);
            }

            securityEntries.Add((authConfig.Name, [.. mergedScopes]));
        }

        return [.. securityEntries];
    }

    static string[] BuildScopes(IEnumerable<AuthorizeAttribute> authorizeAttributes)
    {
        var scopes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var authorizeAttribute in authorizeAttributes)
        {
            if (authorizeAttribute.Roles is not { Length: > 0 } roles)
                continue;

            foreach (var role in roles.Split(','))
                scopes.Add(role);
        }

        return [.. scopes];
    }
}
