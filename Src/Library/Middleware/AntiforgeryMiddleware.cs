using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

sealed class AntiforgeryMiddleware
{
    internal static bool IsRegistered { get; set; }

    readonly RequestDelegate _next;
    readonly IAntiforgery _antiforgery;

    const string UrlEncodedFormContentType = "application/x-www-form-urlencoded";
    const string MultipartFormContentType = "multipart/form-data";

    public AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task Invoke(HttpContext context)
    {
        if (context.Request.Method == HttpMethods.Get ||
            context.Request.Method == HttpMethods.Trace ||
            context.Request.Method == HttpMethods.Options ||
            context.Request.Method == HttpMethods.Head)
        {
            await _next(context);

            return;
        }

        var contentType = context.Request.ContentType;

        if (string.IsNullOrEmpty(contentType))
        {
            await _next(context);

            return;
        }

        if (contentType.Equals(UrlEncodedFormContentType, StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith(MultipartFormContentType, StringComparison.OrdinalIgnoreCase))
        {
            var endpointDefinition = context.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();

            if (endpointDefinition?.AntiforgeryEnabled is true)
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    await context.Response.SendErrorsAsync(
                        new()
                        {
                            new(
                                propertyName: Cfg.ErrOpts.GeneralErrorsField,
                                errorMessage: "Anti-forgery token is invalid!")
                        });

                    return;
                }
            }
        }
        await _next(context);
    }
}