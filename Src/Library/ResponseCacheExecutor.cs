using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

internal static class ResponseCacheExecutor
{
    public static void Execute(HttpContext context, ResponseCacheAttribute? attrib)
    {
        if (attrib is null) return;

        if (!attrib.NoStore && attrib.Duration == 0)
            throw new InvalidOperationException("ResponseCache duration MUST be set unless NoStore is true!");

        var headers = context.Response.Headers;

        headers.Remove(HeaderNames.Vary);
        headers.Remove(HeaderNames.CacheControl);
        headers.Remove(HeaderNames.Pragma);

        if (!string.IsNullOrEmpty(attrib.VaryByHeader))
        {
            headers.Vary = attrib.VaryByHeader;
        }

        if (attrib.VaryByQueryKeys != null)
        {
            var responseCachingFeature = context.Features.Get<IResponseCachingFeature>();
            if (responseCachingFeature == null)
            {
                throw new InvalidOperationException("Please enable response caching middleware!");
            }
            responseCachingFeature.VaryByQueryKeys = attrib.VaryByQueryKeys;
        }

        if (attrib.NoStore)
        {
            headers.CacheControl = "no-store";

            if (attrib.Location == ResponseCacheLocation.None)
            {
                headers.AppendCommaSeparatedValues(HeaderNames.CacheControl, "no-cache");
                headers.Pragma = "no-cache";
            }
        }
        else
        {
            string? cacheControlValue;
            switch (attrib.Location)
            {
                case ResponseCacheLocation.Any:
                    cacheControlValue = "public,";
                    break;
                case ResponseCacheLocation.Client:
                    cacheControlValue = "private,";
                    break;
                case ResponseCacheLocation.None:
                    cacheControlValue = "no-cache,";
                    headers.Pragma = "no-cache";
                    break;
                default:
                    cacheControlValue = null;
                    break;
            }

            cacheControlValue = $"{cacheControlValue}max-age={attrib.Duration}";
            headers.CacheControl = cacheControlValue;
        }
    }
}
