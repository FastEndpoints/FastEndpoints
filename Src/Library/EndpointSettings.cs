using Microsoft.AspNetCore.Builder;

namespace FastEndpoints;

internal class EndpointSettings
{
    internal string[]? Routes;
    internal string[]? Verbs;
    internal bool AllowAnonymous;
    internal bool ThrowIfValidationFailed = true;
    internal string[]? Policies;
    internal string[]? Roles;
    internal string[]? Permissions;
    internal bool AllowAnyPermission;
    internal string[]? Claims;
    internal bool AllowAnyClaim;
    internal bool AllowFileUploads;
    internal Action<RouteHandlerBuilder>? InternalConfigAction;
    internal Action<RouteHandlerBuilder>? UserConfigAction;
}

