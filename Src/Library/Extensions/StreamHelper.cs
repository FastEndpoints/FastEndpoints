using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

internal static class StreamHelper
{
    internal static (RangeItemHeaderValue? range, long rangeLength, bool shouldSendBody)
    ModifyHeaders(HttpContext ctx, string contentType, string? fileName, long? fileLength, bool processRanges, DateTimeOffset? lastModified)
    {
        var request = ctx.Request;
        var reqHeaders = request.GetTypedHeaders();

        if (lastModified is not null)
            lastModified = RoundDownToWholeSeconds(lastModified.Value);

        var preconditionState = PreconditionState(reqHeaders, lastModified);

        var response = ctx.Response;
        SetLastModified(response, lastModified);

        if (preconditionState == Precondition.NotModified)
        {
            response.StatusCode = StatusCodes.Status304NotModified;
            return (range: null, rangeLength: 0, shouldSendBody: false);
        }
        else if (preconditionState == Precondition.PreconditionFailed)
        {
            response.StatusCode = StatusCodes.Status412PreconditionFailed;
            return (range: null, rangeLength: 0, shouldSendBody: false);
        }

        response.ContentType = contentType;

        if (fileName is not null)
        {
            var cdHdr = new ContentDispositionHeaderValue("attachment");
            cdHdr.SetHttpFileName(fileName);
            ctx.Response.Headers.ContentDisposition = cdHdr.ToString();
        }

        if (fileLength is not null)
        {
            response.ContentLength = fileLength.Value;

            if (processRanges)
            {
                response.Headers.AcceptRanges = "bytes";

                if ((request.Method == HttpMethods.Head) || ((request.Method == HttpMethods.Get)
                    && (preconditionState == Precondition.Unspecified || preconditionState == Precondition.ShouldProcess)
                    && IfRangeValid(reqHeaders, lastModified)))
                {
                    return SetRangeHeaders(ctx, reqHeaders, fileLength.Value);
                }
            }
        }

        return (range: null, rangeLength: 0, shouldSendBody: request.Method != HttpMethods.Head);
    }

    internal static async Task WriteFileAsync(HttpContext ctx, Stream stream, RangeItemHeaderValue? range, long rangeLength, CancellationToken cancellation)
    {
        using (stream)
        {
            try
            {
                if (range is null)
                {
                    await StreamCopyOperation.CopyToAsync(stream, ctx.Response.Body, null, 64 * 1024, cancellation);
                }
                else
                {
                    stream.Seek(range.From!.Value, SeekOrigin.Begin);
                    await StreamCopyOperation.CopyToAsync(stream, ctx.Response.Body, rangeLength, 64 * 1024, cancellation);
                }
            }
            catch (OperationCanceledException)
            {
                ctx.Abort();
            }
        }
    }

    private static bool IfRangeValid(RequestHeaders reqHeaders, DateTimeOffset? lastModified)
    {
        var ifRange = reqHeaders.IfRange;
        return ifRange?.LastModified.HasValue != true || !lastModified.HasValue || lastModified <= ifRange.LastModified;
    }

    private static Precondition PreconditionState(RequestHeaders httpRequestHeaders, DateTimeOffset? lastModified)
    {
        const Precondition ifMatchState = Precondition.Unspecified;
        const Precondition ifNoneMatchState = Precondition.Unspecified;
        var ifModifiedSinceState = Precondition.Unspecified;
        var ifUnmodifiedSinceState = Precondition.Unspecified;

        var now = RoundDownToWholeSeconds(DateTimeOffset.UtcNow);

        var ifModifiedSince = httpRequestHeaders.IfModifiedSince;
        if (lastModified.HasValue && ifModifiedSince.HasValue && ifModifiedSince <= now)
        {
            var modified = ifModifiedSince < lastModified;
            ifModifiedSinceState = modified ? Precondition.ShouldProcess : Precondition.NotModified;
        }

        var ifUnmodifiedSince = httpRequestHeaders.IfUnmodifiedSince;
        if (lastModified.HasValue && ifUnmodifiedSince.HasValue && ifUnmodifiedSince <= now)
        {
            var unmodified = ifUnmodifiedSince >= lastModified;
            ifUnmodifiedSinceState = unmodified ? Precondition.ShouldProcess : Precondition.PreconditionFailed;
        }

        return MaxPreconditionState(ifMatchState, ifNoneMatchState, ifModifiedSinceState, ifUnmodifiedSinceState);
    }

    private static (RangeItemHeaderValue? range, long rangeLength, bool serveBody)
    SetRangeHeaders(HttpContext ctx, RequestHeaders reqHeaders, long fileLength)
    {
        var rspHeaders = ctx.Response.GetTypedHeaders();
        var sendBody = ctx.Request.Method != HttpMethods.Head;
        var (reqIsRange, range) = Range.Parse(ctx, reqHeaders, fileLength);

        if (!reqIsRange)
            return (range: null, rangeLength: 0, sendBody);

        if (range == null)
        {
            ctx.Response.StatusCode = StatusCodes.Status416RangeNotSatisfiable;
            rspHeaders.ContentRange = new ContentRangeHeaderValue(fileLength);
            ctx.Response.ContentLength = 0;

            return (range: null, rangeLength: 0, serveBody: false);
        }

        ctx.Response.StatusCode = StatusCodes.Status206PartialContent;
        rspHeaders.ContentRange = new ContentRangeHeaderValue(range.From!.Value, range.To!.Value, fileLength);

        var rangeLength = SetContentLength(ctx.Response, range);

        return (range, rangeLength, sendBody);
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
        var rspHeaders = response.GetTypedHeaders();

        if (lastModified is not null)
            rspHeaders.LastModified = lastModified;
    }

    private static Precondition MaxPreconditionState(params Precondition[] states)
    {
        var max = Precondition.Unspecified;

        for (var i = 0; i < states.Length; i++)
        {
            if (states[i] > max)
                max = states[i];
        }

        return max;
    }

    private static DateTimeOffset RoundDownToWholeSeconds(DateTimeOffset dateTimeOffset)
    {
        var ticksToRemove = dateTimeOffset.Ticks % TimeSpan.TicksPerSecond;
        return dateTimeOffset.Subtract(TimeSpan.FromTicks(ticksToRemove));
    }

    private static class Range
    {
        public static (bool isRangeRequest, RangeItemHeaderValue? range)
        Parse(HttpContext ctx, RequestHeaders reqHeaders, long length)
        {
            var rangeHdr = ctx.Request.Headers.Range;
            if (StringValues.IsNullOrEmpty(rangeHdr))
                return (false, null);

            if (rangeHdr.Count > 1 || (rangeHdr[0] ?? string.Empty).Contains(','))
                return (false, null);

            var rangeHeader = reqHeaders.Range;
            if (rangeHeader == null)
                return (false, null);

            var ranges = rangeHeader.Ranges;
            if (ranges == null)
                return (false, null);

            if (ranges.Count == 0)
                return (true, null);

            if (length == 0)
                return (true, null);

            var range = Normalize(ranges.Single(), length);

            return (true, range);
        }

        internal static RangeItemHeaderValue? Normalize(RangeItemHeaderValue range, long length)
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

    private enum Precondition
    {
        Unspecified,
        NotModified,
        ShouldProcess,
        PreconditionFailed
    }
}