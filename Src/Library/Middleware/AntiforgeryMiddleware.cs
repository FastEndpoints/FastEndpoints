using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

sealed class AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    internal static bool IsRegistered { get; set; }
    internal static Func<HttpContext, bool>? SkipFilter { private get; set; }
    internal static string[] AdditionalContentTypes { private get; set; } = [];

    const string UrlEncodedFormContentType = "application/x-www-form-urlencoded";
    const string MultipartFormContentType = "multipart/form-data";

    public async Task Invoke(HttpContext ctx)
    {
        if (ctx.Request.Method == HttpMethods.Get ||
            ctx.Request.Method == HttpMethods.Trace ||
            ctx.Request.Method == HttpMethods.Options ||
            ctx.Request.Method == HttpMethods.Head ||
            SkipFilter?.Invoke(ctx) is true)
        {
            await next(ctx);

            return;
        }

        var contentType = ctx.Request.ContentType;

        if (string.IsNullOrEmpty(contentType))
        {
            await next(ctx);

            return;
        }

        if (contentType.Equals(UrlEncodedFormContentType, StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith(MultipartFormContentType, StringComparison.OrdinalIgnoreCase) ||
            AdditionalContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            var endpointDefinition = ctx.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();

            if (endpointDefinition?.AntiforgeryEnabled is true)
            {
                try
                {
                    await antiforgery.ValidateRequestAsync(ctx);
                }
                catch (AntiforgeryValidationException)
                {
                    await ctx.Response.SendErrorsAsync(
                    [
                        new(
                            propertyName: Cfg.ErrOpts.GeneralErrorsField,
                            errorMessage: "Anti-forgery token is invalid!")
                    ]);

                    return;
                }
            }
        }
        await next(ctx);
    }
}