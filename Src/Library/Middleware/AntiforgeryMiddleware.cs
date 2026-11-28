using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

sealed class AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    internal static bool IsRegistered { get; set; }
    internal static Func<HttpContext, bool>? SkipFilter { private get; set; }
    internal static string[] AdditionalContentTypes
    {
        set => _additionalMediaTypes = [..value.Select(MediaTypeOf)];
    }

    static string[] _additionalMediaTypes = [];

    const string MultipartFormContentType = "multipart/form-data";

    public async Task Invoke(HttpContext ctx)
    {
        if (ctx.Request.Method == HttpMethods.Get ||
            ctx.Request.Method == HttpMethods.Trace ||
            ctx.Request.Method == HttpMethods.Options ||
            ctx.Request.Method == HttpMethods.Head ||
            ctx.Request.Method == "QUERY" ||
            SkipFilter?.Invoke(ctx) is true)
        {
            await next(ctx);

            return;
        }

        var endpointDefinition = ctx.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();

        // plain-text dtos are bound from the body regardless of the content type (even a missing one), so they're always validated.
        if (endpointDefinition?.AntiforgeryEnabled is true &&
            (endpointDefinition.ReqDtoIsPlainText || HasValidatedContentType(ctx.Request)))
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
        await next(ctx);
    }

    static bool HasValidatedContentType(HttpRequest request)
    {
        var contentType = request.ContentType;

        return !string.IsNullOrEmpty(contentType) &&
               (request.HasFormContentType ||
                contentType.StartsWith(MultipartFormContentType, StringComparison.OrdinalIgnoreCase) ||
                IsAdditionalContentType(contentType));
    }

    static bool IsAdditionalContentType(string contentType)
        => _additionalMediaTypes.Length > 0 &&
           _additionalMediaTypes.Contains(MediaTypeOf(contentType), StringComparer.OrdinalIgnoreCase);

    // strips parameters such as "; charset=utf-8" so they can't bypass validation. unparseable values are compared as is.
    static string MediaTypeOf(string contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var mediaType) && mediaType.MediaType.HasValue
               ? mediaType.MediaType.Value
               : contentType;
}