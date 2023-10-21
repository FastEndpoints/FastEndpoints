using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using System.Net.Mime;

namespace FastEndpoints.Middleware;

internal sealed class AntiforgeryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;

    const string urlEncodedFormContentType = "application/x-www-form-urlencoded";
    const string multipartFormContentType = "multipart/form-data";
    public AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task Invoke(HttpContext context)
    {
        //GET请求不需要防伪验证
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
        if (contentType.Equals(urlEncodedFormContentType, StringComparison.OrdinalIgnoreCase) ||
            contentType.StartsWith(multipartFormContentType, StringComparison.OrdinalIgnoreCase))
        {
            var endpointDefinition = context.GetEndpoint()?.Metadata.GetMetadata<EndpointDefinition>();
            if (endpointDefinition?.IsEnlableAntiforgery is true)
            {
                try
                {
                    await _antiforgery.ValidateRequestAsync(context);
                }
                catch (AntiforgeryValidationException)
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Invalid anti-forgery token");
                    return;
                }
            }
        }
        await _next(context);
    }
}