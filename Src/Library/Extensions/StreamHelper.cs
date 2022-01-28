using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

internal static class StreamHelper
{
    private const string AcceptRangeHeaderValue = "bytes";

    private enum PreconditionState
    {
        Unspecified,
        NotModified,
        ShouldProcess,
        PreconditionFailed
    }

    internal static (RangeItemHeaderValue? range, long rangeLength, bool serveBody)
        SetHeaders(
            HttpContext httpContext, string contentType, string? fileDownloadName, long? fileLength, bool enableRangeProcessing,
            DateTimeOffset? lastModified)
    {
        var request = httpContext.Request;
        var httpRequestHeaders = request.GetTypedHeaders();

        if (lastModified.HasValue)
            lastModified = RoundDownToWholeSeconds(lastModified.Value);

        var preconditionState = GetPreconditionState(httpRequestHeaders, lastModified);

        var response = httpContext.Response;
        SetLastModified(response, lastModified);

        if (preconditionState == PreconditionState.NotModified)
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return (range: null, rangeLength: 0, serveBody: false);
        }
        else if (preconditionState == PreconditionState.PreconditionFailed)
        {
            response.StatusCode = StatusCodes.Status412PreconditionFailed;
            return (range: null, rangeLength: 0, serveBody: false);
        }

        response.ContentType = contentType;

        if (fileDownloadName is not null)
            httpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename={fileDownloadName}");

        if (fileLength.HasValue)
        {
            response.ContentLength = fileLength.Value;

            if (enableRangeProcessing)
            {
                SetAcceptRangeHeader(response);

                if ((HttpMethods.IsHead(request.Method) || HttpMethods.IsGet(request.Method))
                    && (preconditionState == PreconditionState.Unspecified || preconditionState == PreconditionState.ShouldProcess)
                    && IfRangeValid(httpRequestHeaders, lastModified))
                {
                    return SetRangeHeaders(httpContext, httpRequestHeaders, fileLength.Value);
                }
            }
        }

        return (range: null, rangeLength: 0, serveBody: !HttpMethods.IsHead(request.Method));
    }

    internal static async Task WriteFileAsync(HttpContext context, Stream targetStream, RangeItemHeaderValue? range, long rangeLength)
    {
        const int BufferSize = 64 * 1024;
        var outputStream = context.Response.Body;
        using (targetStream)
        {
            try
            {
                if (range is null)
                {
                    await StreamCopyOperation.CopyToAsync(targetStream, outputStream, count: null, bufferSize: 64 * 1024, cancel: context.RequestAborted);
                }
                else
                {
                    targetStream.Seek(range.From!.Value, SeekOrigin.Begin);
                    await StreamCopyOperation.CopyToAsync(targetStream, outputStream, rangeLength, BufferSize, context.RequestAborted);
                }
            }
            catch (OperationCanceledException)
            {
                context.Abort();
            }
        }
    }

    private static bool IfRangeValid(
        RequestHeaders httpRequestHeaders,
        DateTimeOffset? lastModified)
    {
        var ifRange = httpRequestHeaders.IfRange;
        return ifRange?.LastModified.HasValue != true || !lastModified.HasValue || lastModified <= ifRange.LastModified;
    }

    private static PreconditionState GetPreconditionState(
        RequestHeaders httpRequestHeaders,
        DateTimeOffset? lastModified)
    {
        const PreconditionState ifMatchState = PreconditionState.Unspecified;
        const PreconditionState ifNoneMatchState = PreconditionState.Unspecified;
        var ifModifiedSinceState = PreconditionState.Unspecified;
        var ifUnmodifiedSinceState = PreconditionState.Unspecified;

        var now = RoundDownToWholeSeconds(DateTimeOffset.UtcNow);

        var ifModifiedSince = httpRequestHeaders.IfModifiedSince;
        if (lastModified.HasValue && ifModifiedSince.HasValue && ifModifiedSince <= now)
        {
            var modified = ifModifiedSince < lastModified;
            ifModifiedSinceState = modified ? PreconditionState.ShouldProcess : PreconditionState.NotModified;
        }

        var ifUnmodifiedSince = httpRequestHeaders.IfUnmodifiedSince;
        if (lastModified.HasValue && ifUnmodifiedSince.HasValue && ifUnmodifiedSince <= now)
        {
            var unmodified = ifUnmodifiedSince >= lastModified;
            ifUnmodifiedSinceState = unmodified ? PreconditionState.ShouldProcess : PreconditionState.PreconditionFailed;
        }

        return GetMaxPreconditionState(ifMatchState, ifNoneMatchState, ifModifiedSinceState, ifUnmodifiedSinceState);
    }

    private static (RangeItemHeaderValue? range, long rangeLength, bool serveBody)
        SetRangeHeaders(HttpContext httpContext, RequestHeaders httpRequestHeaders, long fileLength)
    {
        var response = httpContext.Response;
        var httpResponseHeaders = response.GetTypedHeaders();
        var serveBody = !HttpMethods.IsHead(httpContext.Request.Method);

        var (isRangeRequest, range) = RangeHelper.ParseRange(
            httpContext,
            httpRequestHeaders,
            fileLength);

        if (!isRangeRequest)
            return (range: null, rangeLength: 0, serveBody);

        if (range == null)
        {
            response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            httpResponseHeaders.ContentRange = new ContentRangeHeaderValue(fileLength);
            response.ContentLength = 0;

            return (range: null, rangeLength: 0, serveBody: false);
        }

        response.StatusCode = StatusCodes.Status206PartialContent;
        httpResponseHeaders.ContentRange = new ContentRangeHeaderValue(
            range.From!.Value,
            range.To!.Value,
            fileLength);

        var rangeLength = SetContentLength(response, range);

        return (range, rangeLength, serveBody);
    }

    private static long SetContentLength(HttpResponse response, RangeItemHeaderValue range)
    {
        var start = range.From!.Value;
        var end = range.To!.Value;
        var length = end - start + 1;
        response.ContentLength = length;
        return length;
    }

    private static void SetLastModified(HttpResponse response, DateTimeOffset? lastModified)
    {
        var httpResponseHeaders = response.GetTypedHeaders();
        if (lastModified.HasValue)
        {
            httpResponseHeaders.LastModified = lastModified;
        }
    }

    private static void SetAcceptRangeHeader(HttpResponse response)
    {
        response.Headers.AcceptRanges = AcceptRangeHeaderValue;
    }

    private static PreconditionState GetMaxPreconditionState(params PreconditionState[] states)
    {
        var max = PreconditionState.Unspecified;
        for (var i = 0; i < states.Length; i++)
        {
            if (states[i] > max)
            {
                max = states[i];
            }
        }
        return max;
    }

    private static DateTimeOffset RoundDownToWholeSeconds(DateTimeOffset dateTimeOffset)
    {
        var ticksToRemove = dateTimeOffset.Ticks % TimeSpan.TicksPerSecond;
        return dateTimeOffset.Subtract(TimeSpan.FromTicks(ticksToRemove));
    }

    private static class RangeHelper
    {
        public static (bool isRangeRequest, RangeItemHeaderValue? range) ParseRange(HttpContext context, RequestHeaders requestHeaders, long length)
        {
            var rawRangeHeader = context.Request.Headers.Range;
            if (StringValues.IsNullOrEmpty(rawRangeHeader))
                return (false, null);

            if (rawRangeHeader.Count > 1 || (rawRangeHeader[0] ?? string.Empty).Contains(','))
                return (false, null);

            var rangeHeader = requestHeaders.Range;
            if (rangeHeader == null)
                return (false, null);

            var ranges = rangeHeader.Ranges;
            if (ranges == null)
                return (false, null);

            if (ranges.Count == 0)
                return (true, null);

            if (length == 0)
                return (true, null);

            var range = NormalizeRange(ranges.Single(), length);

            return (true, range);
        }

        internal static RangeItemHeaderValue? NormalizeRange(RangeItemHeaderValue range, long length)
        {
            var start = range.From;
            var end = range.To;

            if (start.HasValue)
            {
                if (start.Value >= length)
                    return null;

                if (!end.HasValue || end.Value >= length)
                    end = length - 1;
            }
            else if (end.HasValue)
            {
                if (end.Value == 0)
                    return null;

                var bytes = Math.Min(end.Value, length);
                start = length - bytes;
                end = start + bytes - 1;
            }

            return new RangeItemHeaderValue(start, end);
        }
    }
}