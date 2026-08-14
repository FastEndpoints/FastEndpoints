using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

static class ResponseCacheExecutor
{
    public static void Execute(HttpContext context, EndpointDefinition epDef)
    {
        var attrib = epDef.ResponseCacheSettings;

        switch (attrib)
        {
            case null:
                return;
            case { NoStore: false, Duration: 0 }:
                throw new InvalidOperationException("ResponseCache duration MUST be set unless NoStore is true!");
        }

        var cachingFeature = context.Features.Get<IResponseCachingFeature>();

        if (cachingFeature is null) //endpoint specifies caching but middleware not setup correctly
            throw new InvalidOperationException("Please enable response caching middleware!");

        var headers = context.Response.Headers;

        headers.Remove(HeaderNames.Vary);
        headers.Remove(HeaderNames.CacheControl);
        headers.Remove(HeaderNames.Pragma);

        if (!string.IsNullOrEmpty(attrib.VaryByHeader))
            headers.Vary = attrib.VaryByHeader;

        if (attrib.VaryByQueryKeys != null)
            cachingFeature.VaryByQueryKeys = attrib.VaryByQueryKeys;

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
            if (attrib.Location == ResponseCacheLocation.None)
                headers.Pragma = "no-cache";

            headers.CacheControl = epDef.ResponseCacheControl ??= BuildCacheControlValue(attrib);
        }
    }

    static string BuildCacheControlValue(ResponseCacheAttribute attrib)
    {
        var location = attrib.Location switch
        {
            ResponseCacheLocation.Any => "public,",
            ResponseCacheLocation.Client => "private,",
            ResponseCacheLocation.None => "no-cache,",
            _ => null
        };

        return $"{location}max-age={attrib.Duration}";
    }
}