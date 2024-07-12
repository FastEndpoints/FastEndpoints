using Microsoft.AspNetCore.Builder;

namespace FastEndpoints.Security;

public static class JwtRevocationExtensions
{
    /// <summary>
    /// adds an implementation of <see cref="JwtRevocationMiddleware" /> to the pipeline for the purpose of checking incoming jwt bearer tokens for validity.
    /// </summary>
    /// <typeparam name="T">implementation type of the token revocation middleware</typeparam>
    public static IApplicationBuilder UseJwtRevocation<T>(this IApplicationBuilder app) where T : JwtRevocationMiddleware
        => app.UseMiddleware<T>();
}