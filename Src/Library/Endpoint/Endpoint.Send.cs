using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> where TRequest : notnull
{
    /// <summary>
    /// send the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendAsync(TResponse response, int statusCode = 200, CancellationToken cancellation = default)
    {
        _response = response;

        return HttpContext.Response.SendAsync(response, statusCode, Definition.SerializerContext, cancellation);
    }

    /// <summary>
    /// execute and send any <see cref="IResult" /> produced by the <see cref="Results" /> class in minimal apis.
    /// </summary>
    /// <param name="result">
    /// the <see cref="IResult" /> instance to execute such as:
    /// <code>
    /// Results.Forbid();
    /// Results.Ok(...);
    /// </code>
    /// </param>
    protected Task SendResultAsync(IResult result)
        => HttpContext.Response.SendResultAsync(result);

    /// <summary>
    /// sends an object serialized as json to the client. if a response interceptor has been defined,
    /// then that will be executed before the normal response is sent.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    /// <exception cref="InvalidOperationException">will throw if an interceptor has not been defined against the endpoint or globally</exception>
    protected async Task SendInterceptedAsync(object response, int statusCode = 200, CancellationToken cancellation = default)
    {
        if (Definition.ResponseIntrcptr is null)
            throw new InvalidOperationException("Response interceptor has not been configured!");

        await RunResponseInterceptor(Definition.ResponseIntrcptr, response, statusCode, HttpContext, ValidationFailures, cancellation);

        if (!HttpContext.ResponseStarted())
            await HttpContext.Response.SendAsync(response, statusCode, Definition.SerializerContext, cancellation);
    }

    /// <summary>
    /// send a 201 created response with a location header containing where the resource can be retrieved from.
    /// <para>
    /// HINT: if pointing to an endpoint with multiple verbs, make sure to specify the 'verb' argument and if pointing to a multi route endpoint,
    /// specify the 'routeNumber' argument.
    /// </para>
    /// <para>
    /// WARNING: this overload will not add a location header if you've set a custom endpoint name using .WithName() method. use the other overload
    /// that accepts a string endpoint name instead.
    /// </para>
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint where the resource can be retrieved from</typeparam>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="verb">only useful when pointing to a multi verb endpoint</param>
    /// <param name="routeNumber">only useful when pointing to a multi route endpoint</param>
    /// <param name="generateAbsoluteUrl">set to true for generating a absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendCreatedAtAsync<TEndpoint>(object? routeValues,
                                                 TResponse responseBody,
                                                 Http? verb = null,
                                                 int? routeNumber = null,
                                                 bool generateAbsoluteUrl = false,
                                                 CancellationToken cancellation = default) where TEndpoint : IEndpoint
    {
        if (responseBody is not null)
            _response = responseBody;

        return HttpContext.Response.SendCreatedAtAsync<TEndpoint>(
            routeValues,
            responseBody,
            verb,
            routeNumber,
            Definition.SerializerContext,
            generateAbsoluteUrl,
            cancellation);
    }

    /// <summary>
    /// send a 201 created response with a location header containing where the resource can be retrieved from.
    /// <para>
    /// WARNING: this method is only supported on single verb/route endpoints. it will not produce a `Location` header if used in a multi verb or multi
    /// route endpoint.
    /// </para>
    /// </summary>
    /// <param name="endpointName">the name of the endpoint to use for link generation (openapi route id)</param>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="generateAbsoluteUrl">set to true for generating a absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendCreatedAtAsync(string endpointName,
                                      object? routeValues,
                                      TResponse responseBody,
                                      bool generateAbsoluteUrl = false,
                                      CancellationToken cancellation = default)
    {
        if (responseBody is not null)
            _response = responseBody;

        return HttpContext.Response.SendCreatedAtAsync(
            endpointName,
            routeValues,
            responseBody,
            Definition.SerializerContext,
            generateAbsoluteUrl,
            cancellation);
    }

    /// <summary>
    /// send the supplied string content to the client.
    /// </summary>
    /// <param name="content">the string to write to the response body</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="contentType">optional content type header value</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendStringAsync(string content,
                                   int statusCode = 200,
                                   string contentType = "text/plain; charset=utf-8",
                                   CancellationToken cancellation = default)
        => HttpContext.Response.SendStringAsync(content, statusCode, contentType, cancellation);

    /// <summary>
    /// send an http 200 ok response with the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendOkAsync(TResponse response, CancellationToken cancellation = default)
    {
        _response = response;

        return HttpContext.Response.SendOkAsync(response, Definition.SerializerContext, cancellation);
    }

    /// <summary>
    /// send an http 200 ok response without any body
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendOkAsync(CancellationToken cancellation = default)
        => HttpContext.Response.SendOkAsync(cancellation);

    /// <summary>
    /// send a 400 bad request with error details of the current validation failures
    /// </summary>
    /// <param name="statusCode">the status code for the error response</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendErrorsAsync(int statusCode = 400, CancellationToken cancellation = default)
        => HttpContext.Response.SendErrorsAsync(ValidationFailures, statusCode, Definition.SerializerContext, cancellation);

    /// <summary>
    /// send a 204 no content response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendNoContentAsync(CancellationToken cancellation = default)
        => HttpContext.Response.SendNoContentAsync(cancellation);

    /// <summary>
    /// send a 404 not found response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendNotFoundAsync(CancellationToken cancellation = default)
        => HttpContext.Response.SendNotFoundAsync(cancellation);

    /// <summary>
    /// send a 401 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendUnauthorizedAsync(CancellationToken cancellation = default)
        => HttpContext.Response.SendUnauthorizedAsync(cancellation);

    /// <summary>
    /// send a 403 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendForbiddenAsync(CancellationToken cancellation = default)
        => HttpContext.Response.SendForbiddenAsync(cancellation);

    /// <summary>
    /// send a 302/301 redirect response
    /// </summary>
    /// <param name="location">the location to redirect to</param>
    /// <param name="isPermanent">set to true for a 301 redirect. 302 is the default.</param>
    /// <param name="allowRemoteRedirects">set to true if it's ok to redirect to remote addresses, which is prone to open redirect attacks.</param>
    /// <exception cref="InvalidOperationException">thrown if <paramref name="allowRemoteRedirects" /> is not set to true and the supplied url is not local</exception>
    protected Task SendRedirectAsync(string location, bool isPermanent = false, bool allowRemoteRedirects = false)
        => HttpContext.Response.SendRedirectAsync(location, isPermanent, allowRemoteRedirects);

    /// <summary>
    /// send headers in response to a HEAD request
    /// </summary>
    /// <param name="headers">an action to be performed on the headers dictionary of the response</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    protected Task SendHeadersAsync(Action<IHeaderDictionary> headers, int statusCode = 200, CancellationToken cancellation = default)
        => HttpContext.Response.SendHeadersAsync(headers, statusCode, cancellation);

    /// <summary>
    /// send a byte array to the client
    /// </summary>
    /// <param name="bytes">the bytes to send</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendBytesAsync(byte[] bytes,
                                  string? fileName = null,
                                  string contentType = "application/octet-stream",
                                  DateTimeOffset? lastModified = null,
                                  bool enableRangeProcessing = false,
                                  CancellationToken cancellation = default)
        => HttpContext.Response.SendBytesAsync(bytes, fileName, contentType, lastModified, enableRangeProcessing, cancellation);

    /// <summary>
    /// send a file to the client
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendFileAsync(FileInfo fileInfo,
                                 string contentType = "application/octet-stream",
                                 DateTimeOffset? lastModified = null,
                                 bool enableRangeProcessing = false,
                                 CancellationToken cancellation = default)
        => HttpContext.Response.SendFileAsync(fileInfo, contentType, lastModified, enableRangeProcessing, cancellation);

    /// <summary>
    /// send the contents of a stream to the client
    /// </summary>
    /// <param name="stream">the stream to read the data from</param>
    /// <param name="fileName">and optional file name to set in the content-disposition header</param>
    /// <param name="fileLengthBytes">optional total size of the file/stream</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendStreamAsync(Stream stream,
                                   string? fileName = null,
                                   long? fileLengthBytes = null,
                                   string contentType = "application/octet-stream",
                                   DateTimeOffset? lastModified = null,
                                   bool enableRangeProcessing = false,
                                   CancellationToken cancellation = default)
        => HttpContext.Response.SendStreamAsync(
            stream,
            fileName,
            fileLengthBytes,
            contentType,
            lastModified,
            enableRangeProcessing,
            cancellation);

    /// <summary>
    /// start a "server-sent-events" data stream for the client asynchronously without blocking any threads
    /// </summary>
    /// <typeparam name="T">the type of the objects being sent in the event stream</typeparam>
    /// <param name="eventName">the name of the event stream</param>
    /// <param name="eventStream">an IAsyncEnumerable that is the source of the data</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    protected Task SendEventStreamAsync<T>(string eventName, IAsyncEnumerable<T> eventStream, CancellationToken cancellation = default)
        => HttpContext.Response.SendEventStreamAsync(eventName, eventStream, cancellation);

    /// <summary>
    /// send an empty json object in the body
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    protected Task SendEmptyJsonObject(CancellationToken cancellation = default)
        => HttpContext.Response.SendEmptyJsonObject(null, cancellation);
}