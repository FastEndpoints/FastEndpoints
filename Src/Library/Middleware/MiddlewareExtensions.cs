using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

public static class MiddlewareExtensions
{
    /// <summary>
    /// enable anti-forgery token verification middleware.
    /// make sure to also add the anti-forgery services with <c>builder.Services.AddAntiForgery()</c>
    /// </summary>
    /// <param name="skipRequestFilter">
    /// an optional predicate which can be used to skip anti-forgery checks for requests that satisfy a given condition.
    /// provide a function that returns <c>true</c> for requests that you'd want the anti-forgery middleware to skip processing.
    /// </param>
    public static IApplicationBuilder UseAntiforgeryFE(this IApplicationBuilder app, Func<HttpContext, bool>? skipRequestFilter = null)
    {
        app.UseMiddleware<AntiforgeryMiddleware>();
        AntiforgeryMiddleware.IsRegistered = true;
        AntiforgeryMiddleware.SkipFilter = skipRequestFilter;

        return app;
    }
}