﻿using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FastEndpoints.Security;

/// <summary>
/// static class for easy cookie based auth
/// </summary>
public static class CookieAuth
{
    /// <summary>
    /// creates the auth cookie and adds it to the current http response
    /// </summary>
    /// <param name="privileges">the privileges to be assigned to the user such as claims, permissions, and roles</param>
    /// <param name="properties">an optional action to configure authentication properties</param>
    /// <exception cref="InvalidOperationException">thrown if the auth middleware hasn't been configure or method is used outside the scope of an http request</exception>
    public static Task SignInAsync(Action<UserPrivileges> privileges, Action<AuthenticationProperties>? properties = null)
    {
        var svc = Cfg.ServiceResolver.TryResolve<IAuthenticationService>() ?? throw new InvalidOperationException("Authentication middleware has not been configured!");

        var ctx = Cfg.ServiceResolver.TryResolve<IHttpContextAccessor>()?.HttpContext ??
                  throw new InvalidOperationException("This operation is only valid during an http request!");

        var privs = new UserPrivileges();
        privileges(privs);

        var claimList = new List<Claim>();

        if (privs.Claims.Count > 0)
            claimList.AddRange(privs.Claims);

        if (privs.Permissions.Count > 0)
            claimList.AddRange(privs.Permissions.Select(p => new Claim(Cfg.SecOpts.PermissionsClaimType, p)));

        if (privs.Roles.Count > 0)
            claimList.AddRange(privs.Roles.Select(r => new Claim(Cfg.SecOpts.RoleClaimType, r)));

        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = (Cfg.ServiceResolver.TryResolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow()
        };

        properties?.Invoke(props);

        return svc.SignInAsync(
            context: ctx,
            scheme: CookieAuthenticationDefaults.AuthenticationScheme,
            principal: new(new ClaimsIdentity(claimList, CookieAuthenticationDefaults.AuthenticationScheme, null, Cfg.SecOpts.RoleClaimType)),
            properties: props);
    }

    /// <summary>
    /// signs the user out from the cookie authentication scheme
    /// </summary>
    /// <param name="properties">an optional action to configure authentication properties</param>
    /// <exception cref="InvalidOperationException">thrown if the auth middleware hasn't been configured or method is used outside the scope of a http request</exception>
    public static Task SignOutAsync(Action<AuthenticationProperties>? properties = null)
    {
        var svc = Cfg.ServiceResolver.TryResolve<IAuthenticationService>() ?? throw new InvalidOperationException("Authentication middleware has not been configured!");

        var ctx = Cfg.ServiceResolver.TryResolve<IHttpContextAccessor>()?.HttpContext ??
                  throw new InvalidOperationException("This operation is only valid during an http request!");

        var props = new AuthenticationProperties
        {
            IsPersistent = true,
            IssuedUtc = (Cfg.ServiceResolver.TryResolve<TimeProvider>() ?? TimeProvider.System).GetUtcNow()
        };

        properties?.Invoke(props);

        return svc.SignOutAsync(ctx, CookieAuthenticationDefaults.AuthenticationScheme, props);
    }
}