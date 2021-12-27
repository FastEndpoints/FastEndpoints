using Microsoft.AspNetCore.Http;

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
        return HttpContext.Response.SendAsync(response, statusCode, cancellation);
    }

    /// <summary>
    /// send the supplied string content to the client.
    /// </summary>
    /// <param name="content">the string to write to the response body</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendStringAsync(string content, int statusCode = 200, CancellationToken cancellation = default)
    {
        return HttpContext.Response.SendStringAsync(content, statusCode, cancellation);
    }

    /// <summary>
    /// send an http 200 ok response without any body
    /// </summary>
    protected Task SendOkAsync()
    {
        return HttpContext.Response.SendOkAsync();
    }

    /// <summary>
    /// send a 400 bad request with error details of the current validation failures
    /// </summary>
    /// <param name="cancellation"></param>
    protected Task SendErrorsAsync(CancellationToken cancellation = default)
    {
        return HttpContext.Response.SendErrorsAsync(ValidationFailures, cancellation);
    }

    /// <summary>
    /// send a 204 no content response
    /// </summary>
    protected Task SendNoContentAsync()
    {
        return HttpContext.Response.SendNoContentAsync();
    }

    /// <summary>
    /// send a 404 not found response
    /// </summary>
    protected Task SendNotFoundAsync()
    {
        return HttpContext.Response.SendNotFoundAsync();
    }

    /// <summary>
    /// send a 401 unauthorized response
    /// </summary>
    protected Task SendUnauthorizedAsync()
    {
        return HttpContext.Response.SendUnauthorizedAsync();
    }

    /// <summary>
    /// send a 403 unauthorized response
    /// </summary>
    protected Task SendForbiddenAsync()
    {
        return HttpContext.Response.SendForbiddenAsync();
    }

    /// <summary>
    /// send a byte array to the client
    /// </summary>
    /// <param name="bytes">the bytes to send</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected async Task SendBytesAsync(byte[] bytes, string? fileName = null, string contentType = "application/octet-stream", CancellationToken cancellation = default)
    {
        await HttpContext.Response.SendBytesAsync(bytes, fileName, contentType, cancellation).ConfigureAwait(false);
    }

    /// <summary>
    /// send a file to the client
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendFileAsync(FileInfo fileInfo, string contentType = "application/octet-stream", CancellationToken cancellation = default)
    {
        return HttpContext.Response.SendFileAsync(fileInfo, contentType, cancellation);
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
        return HttpContext.Response.SendStreamAsync(stream, fileName, fileLengthBytes, contentType, cancellation);
    }

    /// <summary>
    /// send an empty json object in the body
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    protected Task SendEmptyJsonObject(CancellationToken cancellation = default)
    {
        return HttpContext.Response.SendEmptyJsonObject(cancellation);
    }
}

