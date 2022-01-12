using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

internal static class ResponseCacheExecutor
{
    public static void Execute(HttpContext context, ResponseCacheAttribute? rcAttrib)
    {
        if (rcAttrib is null) return;

        if (!rcAttrib.NoStore && rcAttrib.Duration == 0)
            throw new InvalidOperationException("ResponseCache duration MUST be set unless NoStore is true!");

        var headers = context.Response.Headers;

        headers.Remove(HeaderNames.Vary);
        headers.Remove(HeaderNames.CacheControl);
        headers.Remove(HeaderNames.Pragma);

        if (!string.IsNullOrEmpty(rcAttrib.VaryByHeader))
        {
            headers.Vary = rcAttrib.VaryByHeader;
        }

        if (rcAttrib.VaryByQueryKeys != null)
        {
            var responseCachingFeature = context.Features.Get<IResponseCachingFeature>();
            if (responseCachingFeature == null)
            {
                throw new InvalidOperationException("Please enable response caching middleware!");
            }
            responseCachingFeature.VaryByQueryKeys = rcAttrib.VaryByQueryKeys;
        }

        if (rcAttrib.NoStore)
        {
            headers.CacheControl = "no-store";

            if (rcAttrib.Location == ResponseCacheLocation.None)
            {
                headers.AppendCommaSeparatedValues(HeaderNames.CacheControl, "no-cache");
                headers.Pragma = "no-cache";
            }
        }
        else
        {
            string? cacheControlValue;
            switch (rcAttrib.Location)
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

            cacheControlValue = $"{cacheControlValue}max-age={rcAttrib.Duration}";
            headers.CacheControl = cacheControlValue;
        }
    }
}
