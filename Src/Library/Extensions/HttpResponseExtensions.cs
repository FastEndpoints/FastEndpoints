using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json.Nodes;
using static FastEndpoints.Config;

namespace FastEndpoints;

public static class HttpResponseExtensions
{
    /// <summary>
    /// send the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendAsync<TResponse>(this HttpResponse rsp, TResponse response, int statusCode = 200, CancellationToken cancellation = default) where TResponse : notnull
    {
        rsp.StatusCode = statusCode;
        return RespSerializerFunc(rsp, response, "application/json", cancellation);
    }

    /// <summary>
    /// send a 201 created response with a location header containing where the resource can be retrieved from.
    /// <para>WARNING: this method is only supported on single verb/route endpoints. it will not produce a `Location` header if used in a multi verb or multi route endpoint.</para>
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint where the resource can be retrieved from</typeparam>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="cancellation">cancellation token</param>
    public static Task SendCreatedAtAsync<TEndpoint>(this HttpResponse rsp, object? routeValues, object? responseBody, CancellationToken cancellation = default) where TEndpoint : IEndpoint
    {
        return SendCreatedAtAsync(rsp, typeof(TEndpoint).SantizedName(), routeValues, responseBody, cancellation);
    }

    /// <summary>
    /// send a 201 created response with a location header containing where the resource can be retrieved from.
    /// <para>WARNING: this method is only supported on single verb/route endpoints. it will not produce a `Location` header if used in a multi verb or multi route endpoint.</para>
    /// </summary>
    /// <param name="endpointName">the name of the endpoint to use for link generation (openapi route id)</param>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="cancellation">cancellation token</param>
    public static Task SendCreatedAtAsync(this HttpResponse rsp, string endpointName, object? routeValues, object? responseBody, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 201;
        rsp.Headers.Location = rsp.HttpContext.RequestServices
            .GetRequiredService<LinkGenerator>()
            .GetPathByName(endpointName, routeValues);
        return responseBody is null
            ? rsp.StartAsync(cancellation)
            : RespSerializerFunc(rsp, responseBody, "application/json", cancellation);
    }

    /// <summary>
    /// send the supplied string content to the client.
    /// </summary>
    /// <param name="content">the string to write to the response body</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendStringAsync(this HttpResponse rsp, string content, int statusCode = 200, CancellationToken cancellation = default)
    {
        rsp.StatusCode = statusCode;
        rsp.ContentType = "text/plain";
        return rsp.WriteAsync(content, cancellation);
    }

    /// <summary>
    /// send an http 200 ok response without any body
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendOkAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 200;
        return rsp.StartAsync(cancellation);
    }

    /// <summary>
    /// send a 400 bad request with error details of the current validation failures
    /// </summary>
    /// <param name="cancellation"></param>
    public static Task SendErrorsAsync(this HttpResponse rsp, List<ValidationFailure> failures, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 400;
        return RespSerializerFunc(rsp, ErrRespBldrFunc(failures), "application/problem+json", cancellation);
    }

    /// <summary>
    /// send a 204 no content response
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendNoContentAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 204;
        return rsp.StartAsync(cancellation);
    }

    /// <summary>
    /// send a 404 not found response
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendNotFoundAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 404;
        return rsp.StartAsync(cancellation);
    }

    /// <summary>
    /// send a 401 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendUnauthorizedAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 401;
        return rsp.StartAsync(cancellation);
    }

    /// <summary>
    /// send a 403 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendForbiddenAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 403;
        return rsp.StartAsync(cancellation);
    }

    /// <summary>
    /// send a byte array to the client
    /// </summary>
    /// <param name="bytes">the bytes to send</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token</param>
    public static async Task SendBytesAsync(this HttpResponse rsp,
        byte[] bytes, string? fileName = null, string contentType = "application/octet-stream", DateTimeOffset? lastModified = null,
        bool enableRangeProcessing = false, CancellationToken cancellation = default)
    {
        using var memoryStream = new MemoryStream(bytes);
        await SendStreamAsync(rsp, memoryStream, fileName, bytes.Length, contentType, lastModified, enableRangeProcessing, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// send a file to the client
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendFileAsync(this HttpResponse rsp,
        FileInfo fileInfo, string contentType = "application/octet-stream", DateTimeOffset? lastModified = null,
        bool enableRangeProcessing = false, CancellationToken cancellation = default)
    {
        return SendStreamAsync(rsp, fileInfo.OpenRead(), fileInfo.Name, fileInfo.Length, contentType, lastModified, enableRangeProcessing, cancellation);
    }

    /// <summary>
    /// send the contents of a stream to the client
    /// </summary>
    /// <param name="stream">the stream to read the data from</param>
    /// <param name="fileName">and optional file name to set in the content-disposition header</param>
    /// <param name="fileLengthBytes">optional total size of the file/stream</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token</param>
    public static async Task SendStreamAsync(this HttpResponse rsp,
        Stream stream, string? fileName = null, long? fileLengthBytes = null, string contentType = "application/octet-stream",
        DateTimeOffset? lastModified = null, bool enableRangeProcessing = false, CancellationToken cancellation = default)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream), "The supplied stream cannot be null!");

        rsp.StatusCode = 200;

        using (stream)
        {
            long? fileLength = fileLengthBytes;

            if (stream.CanSeek)
                fileLength = stream.Length;

            var (range, rangeLength, serveBody) = StreamHelper.SetHeaders(
                rsp.HttpContext, contentType, fileName, fileLength, enableRangeProcessing, lastModified);

            if (!serveBody)
                return;

            await StreamHelper.WriteFileAsync(rsp.HttpContext, stream, range, rangeLength);
        }
    }

    /// <summary>
    /// send an empty json object in the body
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public static Task SendEmptyJsonObject(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.StatusCode = 200;
        return RespSerializerFunc(rsp, new JsonObject(), "application/json", cancellation);
    }
}