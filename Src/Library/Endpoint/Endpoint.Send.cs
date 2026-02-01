using System.Diagnostics.CodeAnalysis;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// target this interface type for creating your own custom response sending methods.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
public interface IResponseSender
{
    /// <summary>
    /// the http context of the current request
    /// </summary>
    HttpContext HttpContext { get; }

    /// <summary>
    /// validation failures collection for the endpoint
    /// </summary>
    List<ValidationFailure> ValidationFailures { get; }

    /// <summary>
    /// gets the endpoint definition which contains all the configuration info for the endpoint
    /// </summary>
    EndpointDefinition Definition { get; }
}

/// <summary>
/// this struct encapsulates the default response sending methods for endpoints.
/// you can add your own custom send methods by writing extension methods targeting <see cref="IResponseSender" /> interface.
/// </summary>
public readonly struct ResponseSender<TRequest, TResponse>(Endpoint<TRequest, TResponse> ep) : IResponseSender where TRequest : notnull
{
    //NOTE: being a struct here reduces gc pressure (verified with benchmarks)

    public HttpContext HttpContext => ep.HttpContext;
    public List<ValidationFailure> ValidationFailures => ep.ValidationFailures;
    public EndpointDefinition Definition => ep.Definition;

    /// <summary>
    /// send a 202 accepted response with a location header containing where the resource can be retrieved from.
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
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> AcceptedAtAsync<TEndpoint>(object? routeValues = null,
                                                 TResponse? responseBody = default,
                                                 Http? verb = null,
                                                 int? routeNumber = null,
                                                 bool generateAbsoluteUrl = false,
                                                 CancellationToken cancellation = default) where TEndpoint : IEndpoint
    {
        if (responseBody is not null)
            ep.Response = responseBody;

        return ep.HttpContext.Response.SendAcceptedAtAsync<TEndpoint>(
            routeValues,
            responseBody,
            verb,
            routeNumber,
            ep.Definition.SerializerContext,
            generateAbsoluteUrl,
            cancellation);
    }

    /// <summary>
    /// send a 202 accepted response with a location header containing where the resource can be retrieved from.
    /// <para>
    /// WARNING: this method is only supported on single verb/route endpoints. it will not produce a `Location` header if used in a multi verb or multi
    /// route endpoint.
    /// </para>
    /// </summary>
    /// <param name="endpointName">the name of the endpoint to use for link generation (openapi route id)</param>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> AcceptedAtAsync(string endpointName,
                                      object? routeValues = null,
                                      TResponse? responseBody = default,
                                      bool generateAbsoluteUrl = false,
                                      CancellationToken cancellation = default)
    {
        if (responseBody is not null)
            ep.Response = responseBody;

        return ep.HttpContext.Response.SendAcceptedAtAsync(
            endpointName,
            routeValues,
            responseBody,
            ep.Definition.SerializerContext,
            generateAbsoluteUrl,
            cancellation);
    }

    /// <summary>
    /// send a byte array to the client
    /// </summary>
    /// <param name="bytes">the bytes to send</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> BytesAsync(byte[] bytes,
                                 string? fileName = null,
                                 string contentType = "application/octet-stream",
                                 DateTimeOffset? lastModified = null,
                                 bool enableRangeProcessing = false,
                                 CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendBytesAsync(bytes, fileName, contentType, lastModified, enableRangeProcessing, cancellation);

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
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> CreatedAtAsync<TEndpoint>(object? routeValues = null,
                                                TResponse? responseBody = default,
                                                Http? verb = null,
                                                int? routeNumber = null,
                                                bool generateAbsoluteUrl = false,
                                                CancellationToken cancellation = default) where TEndpoint : IEndpoint
    {
        if (responseBody is not null)
            ep.Response = responseBody;

        return ep.HttpContext.Response.SendCreatedAtAsync<TEndpoint>(
            routeValues,
            responseBody,
            verb,
            routeNumber,
            ep.Definition.SerializerContext,
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
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> CreatedAtAsync(string endpointName,
                                     object? routeValues = null,
                                     TResponse? responseBody = default,
                                     bool generateAbsoluteUrl = false,
                                     CancellationToken cancellation = default)
    {
        if (responseBody is not null)
            ep.Response = responseBody;

        return ep.HttpContext.Response.SendCreatedAtAsync(
            endpointName,
            routeValues,
            responseBody,
            ep.Definition.SerializerContext,
            generateAbsoluteUrl,
            cancellation);
    }

    /// <summary>
    /// send an empty json object in the body
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> EmptyJsonObject(CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendEmptyJsonObject(null, cancellation);

    /// <summary>
    /// send a 400 bad request with error details of the current validation failures
    /// </summary>
    /// <param name="statusCode">the status code for the error response</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> ErrorsAsync(int statusCode = 400, CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendErrorsAsync(ep.ValidationFailures, statusCode, ep.Definition.SerializerContext, cancellation);

    /// <summary>
    /// start an asynchronous "server-sent-events" data stream for the client with items of the same type, without blocking any threads
    /// </summary>
    /// <typeparam name="T">the type of the objects being sent in the event stream</typeparam>
    /// <param name="eventName">the name of the event stream</param>
    /// <param name="eventStream">an IAsyncEnumerable that is the source of the data</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public Task<Void> EventStreamAsync<T>(string eventName, IAsyncEnumerable<T> eventStream, CancellationToken cancellation = default) where T : notnull
        => ep.HttpContext.Response.SendEventStreamAsync(eventName, eventStream, cancellation);

    /// <summary>
    /// start an asynchronous "server-sent-events" data stream for the client with items that might be of different types, without blocking any threads
    /// </summary>
    /// <param name="eventStream">an IAsyncEnumerable of stream items that is the source of the data</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public Task<Void> EventStreamAsync(IAsyncEnumerable<StreamItem> eventStream, CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendEventStreamAsync(eventStream, cancellation);

    /// <summary>
    /// send a file to the client
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> FileAsync(FileInfo fileInfo,
                                string contentType = "application/octet-stream",
                                DateTimeOffset? lastModified = null,
                                bool enableRangeProcessing = false,
                                CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendFileAsync(fileInfo, contentType, lastModified, enableRangeProcessing, cancellation);

    /// <summary>
    /// send a 403 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> ForbiddenAsync(CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendForbiddenAsync(cancellation);

    /// <summary>
    /// send headers in response to a HEAD request
    /// </summary>
    /// <param name="headers">an action to be performed on the headers dictionary of the response</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public Task<Void> HeadersAsync(Action<IHeaderDictionary> headers, int statusCode = 200, CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendHeadersAsync(headers, statusCode, cancellation);

    /// <summary>
    /// sends an object serialized as json to the client. if a response interceptor has been defined,
    /// then that will be executed before the normal response is sent.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    /// <exception cref="InvalidOperationException">will throw if an interceptor has not been defined against the endpoint or globally</exception>
    public async Task<Void> InterceptedAsync(object response, int statusCode = 200, CancellationToken cancellation = default)
    {
        if (ep.Definition.ResponseIntrcptr is null)
            throw new InvalidOperationException("Response interceptor has not been configured!");

        await Endpoint<TRequest, TResponse>.RunResponseInterceptor(
            ep.Definition.ResponseIntrcptr,
            response,
            statusCode,
            ep.HttpContext,
            ep.ValidationFailures,
            cancellation);

        if (!ep.HttpContext.ResponseStarted())
            await ep.HttpContext.Response.SendAsync(response, statusCode, ep.Definition.SerializerContext, cancellation);

        return Void.Instance;
    }

    /// <summary>
    /// send a 204 no content response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> NoContentAsync(CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendNoContentAsync(cancellation);

    /// <summary>
    /// send a 304 not modified response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> NotModifiedAsync(CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendNotModifiedAsync(cancellation);

    /// <summary>
    /// send a 404 not found response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> NotFoundAsync(CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendNotFoundAsync(cancellation);

    /// <summary>
    /// send an http 200 ok response with the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> OkAsync(TResponse response, CancellationToken cancellation)
    {
        ep.Response = response;

        return ep.HttpContext.Response.SendOkAsync(response, ep.Definition.SerializerContext, cancellation);
    }

    /// <summary>
    /// send an http 200 ok response with the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
#if NET9_0_OR_GREATER
    [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
#endif
    public Task<Void> OkAsync(TResponse response)
        => OkAsync(response, CancellationToken.None);

    /// <summary>
    /// send an http 200 ok response without a body.
    /// </summary>
    /// <param name="cancellation">cancellation token</param>
    public Task<Void> OkAsync(CancellationToken cancellation)
        => ep.HttpContext.Response.SendOkAsync(cancellation);

    /// <summary>
    /// send an http 200 ok response without a body.
    /// </summary>
    public Task<Void> OkAsync()
        => ep.HttpContext.Response.SendOkAsync();

    /// <summary>
    /// send a 302/301 redirect response
    /// </summary>
    /// <param name="location">the location to redirect to</param>
    /// <param name="isPermanent">set to true for a 301 redirect. 302 is the default.</param>
    /// <param name="allowRemoteRedirects">set to true if it's ok to redirect to remote addresses, which is prone to open redirect attacks.</param>
    /// <exception cref="InvalidOperationException">thrown if <paramref name="allowRemoteRedirects" /> is not set to true and the supplied url is not local</exception>
    public Task<Void> RedirectAsync(string location, bool isPermanent = false, bool allowRemoteRedirects = false)
        => ep.HttpContext.Response.SendRedirectAsync(location, isPermanent, allowRemoteRedirects);

    /// <summary>
    /// send the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> ResponseAsync(TResponse response, int statusCode = 200, CancellationToken cancellation = default)
    {
        ep.Response = response;

        return ep.HttpContext.Response.SendAsync(response, statusCode, ep.Definition.SerializerContext, cancellation);
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
    public Task<Void> ResultAsync(IResult result)
        => ep.HttpContext.Response.SendResultAsync(result);

    /// <summary>
    /// send any http status code
    /// </summary>
    /// <param name="statusCode">the http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public Task<Void> StatusCodeAsync(int statusCode, CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendStatusCodeAsync(statusCode, cancellation);

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
    public Task<Void> StreamAsync(Stream stream,
                                  string? fileName = null,
                                  long? fileLengthBytes = null,
                                  string contentType = "application/octet-stream",
                                  DateTimeOffset? lastModified = null,
                                  bool enableRangeProcessing = false,
                                  CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendStreamAsync(stream, fileName, fileLengthBytes, contentType, lastModified, enableRangeProcessing, cancellation);

    /// <summary>
    /// send the supplied string content to the client.
    /// </summary>
    /// <param name="content">the string to write to the response body</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="contentType">optional content type header value</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> StringAsync(string content, int statusCode = 200, string contentType = "text/plain; charset=utf-8", CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendStringAsync(content, statusCode, contentType, cancellation);

    /// <summary>
    /// send a 401 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used</param>
    public Task<Void> UnauthorizedAsync(CancellationToken cancellation = default)
        => ep.HttpContext.Response.SendUnauthorizedAsync(cancellation);
}