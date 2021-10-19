using Microsoft.AspNetCore.Http;
using System.Text.Json.Nodes;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// send the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendAsync(TResponse response, int statusCode = 200, CancellationToken cancellation = default)
    {
        Response = response;
        HttpContext.Response.StatusCode = statusCode;
        return HttpContext.Response.WriteAsJsonAsync(response, SerializerOptions, cancellation);
    }
    /// <summary>
    /// send the supplied string content to the client.
    /// </summary>
    /// <param name="content">the string to write to the response body</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendStringAsync(string content, int statusCode = 200, CancellationToken cancellation = default)
    {
        HttpContext.Response.StatusCode = statusCode;
        HttpContext.Response.ContentType = "text/plain";
        return HttpContext.Response.WriteAsync(content, cancellation);
    }
    /// <summary>
    /// send an http 200 ok response without any body
    /// </summary>
    protected Task SendOkAsync()
    {
        HttpContext.Response.StatusCode = 200;
        return Task.CompletedTask;
    }
    /// <summary>
    /// send a 400 bad request with error details of the current validation failures
    /// </summary>
    /// <param name="cancellation"></param>
    protected Task SendErrorsAsync(CancellationToken cancellation = default)
    {
        HttpContext.Response.StatusCode = 400;
        return HttpContext.Response.WriteAsJsonAsync(new ErrorResponse(ValidationFailures), SerializerOptions, cancellation);
    }
    /// <summary>
    /// send a 204 no content response
    /// </summary>
    protected Task SendNoContentAsync()
    {
        HttpContext.Response.StatusCode = 204;
        return Task.CompletedTask;
    }
    /// <summary>
    /// send a 404 not found response
    /// </summary>
    protected Task SendNotFoundAsync()
    {
        HttpContext.Response.StatusCode = 404;
        return Task.CompletedTask;
    }
    /// <summary>
    /// send a 401 unauthorized response
    /// </summary>
    protected Task SendUnauthorizedAsync()
    {
        HttpContext.Response.StatusCode = 401;
        return Task.CompletedTask;
    }
    /// <summary>
    /// send a 403 unauthorized response
    /// </summary>
    protected Task SendForbiddenAsync()
    {
        HttpContext.Response.StatusCode = 403;
        return Task.CompletedTask;
    }
    /// <summary>
    /// send a byte array to the client
    /// </summary>
    /// <param name="bytes">the bytes to send</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected async Task SendBytesAsync(byte[] bytes, string? fileName = null, string contentType = "application/octet-stream", CancellationToken cancellation = default)
    {
        using var memoryStream = new MemoryStream(bytes);
        await SendStreamAsync(memoryStream, fileName, bytes.Length, contentType, cancellation).ConfigureAwait(false);
    }
    /// <summary>
    /// send a file to the client
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendFileAsync(FileInfo fileInfo, string contentType = "application/octet-stream", CancellationToken cancellation = default)
    {
        return SendStreamAsync(fileInfo.OpenRead(), fileInfo.Name, fileInfo.Length, contentType, cancellation);
    }
    /// <summary>
    /// send the contents of a stream to the client
    /// </summary>
    /// <param name="stream">the stream to read the data from</param>
    /// <param name="fileName">and optional file name to set in the content-disposition header</param>
    /// <param name="fileLengthBytes">optional total size of the file/stream</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendStreamAsync(Stream stream, string? fileName = null, long? fileLengthBytes = null, string contentType = "application/octet-stream", CancellationToken cancellation = default)
    {
        HttpContext.Response.StatusCode = 200;
        HttpContext.Response.ContentType = contentType;
        HttpContext.Response.ContentLength = fileLengthBytes;

        if (fileName is not null)
            HttpContext.Response.Headers.Add("Content-Disposition", $"attachment; filename={fileName}");

        return HttpContext.WriteToResponseAsync(stream, cancellation == default ? HttpContext.RequestAborted : cancellation);
    }
    /// <summary>
    /// send an empty json object in the body
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendEmptyJsonObject(CancellationToken cancellation = default)
    {
        HttpContext.Response.StatusCode = 200;
        return HttpContext.Response.WriteAsJsonAsync(new JsonObject(), SerializerOptions, cancellation);
    }
}

