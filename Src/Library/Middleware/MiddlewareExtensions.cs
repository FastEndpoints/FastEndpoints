using Microsoft.AspNetCore.Builder;

namespace FastEndpoints;

public static class MiddlewareExtensions
{
    /// <summary>
    /// enable anti-forgery token verification middleware.
    /// make sure to also add the anti-forgery services with <c>builder.Services.AddAntiForgery()</c>
    /// </summary>
    public static IApplicationBuilder UseAntiforgeryFE(this IApplicationBuilder app)
    {
        app.UseMiddleware<AntiforgeryMiddleware>();
        AntiforgeryMiddleware.IsRegistered = true;

        return app;
    }
}