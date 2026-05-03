using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints.Agents;

static class AgentVisibilityFilters
{
    internal static readonly Func<EndpointDefinition, ClaimsPrincipal, HttpContext, bool> AuthenticatedCallersOnly =
        static (_, principal, _) => principal.Identity?.IsAuthenticated == true;
}
