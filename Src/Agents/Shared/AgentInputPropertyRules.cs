using System.Reflection;

namespace FastEndpoints.Agents;

static class AgentInputPropertyRules
{
    internal static bool ShouldIgnoreClientInput(PropertyInfo prop)
        => prop.GetCustomAttribute<HasPermissionAttribute>() is not null ||
           prop.GetCustomAttribute<FromClaimAttribute>() is { IsRequired: true } or { RemoveFromSchema: true } ||
           prop.GetCustomAttribute<FromHeaderAttribute>() is { RemoveFromSchema: true } ||
           prop.GetCustomAttribute<FromCookieAttribute>() is { RemoveFromSchema: true };
}
