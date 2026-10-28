using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;

namespace FastEndpoints.Security;

public static class JwtRevocationExtensions
{
    /// <summary>
    /// adds an implementation of <see cref="JwtRevocationMiddleware" /> to the pipeline for the purpose of checking incoming jwt bearer tokens for validity.
    /// register after routing and before authentication to check tokens accepted from the query string or a cookie.
    /// requests without a selected endpoint receive only the authorization header check.
    /// </summary>
    /// <typeparam name="T">implementation type of the token revocation middleware</typeparam>
    public static IApplicationBuilder UseJwtRevocation<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] T>(this IApplicationBuilder app) where T : JwtRevocationMiddleware
        => app.UseMiddleware<T>();
}