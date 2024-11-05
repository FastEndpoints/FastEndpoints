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
    /// <param name="additionalContentTypes">
    /// optional array of additional content-types to enforce antiforgery checks for (if the endpoint has enabled antiforgery).
    /// </param>
    public static IApplicationBuilder UseAntiforgeryFE(this IApplicationBuilder app,
                                                       Func<HttpContext, bool>? skipRequestFilter = null,
                                                       string[]? additionalContentTypes = null)
    {
        app.UseMiddleware<AntiforgeryMiddleware>();
        AntiforgeryMiddleware.IsRegistered = true;
        AntiforgeryMiddleware.SkipFilter = skipRequestFilter;
        if (additionalContentTypes is not null)
            AntiforgeryMiddleware.AdditionalContentTypes = additionalContentTypes;

        return app;
    }
}