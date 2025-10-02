using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static FastEndpoints.Config;

namespace FastEndpoints;

public static class HttpResponseExtensions
{
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
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendAcceptedAtAsync<TEndpoint>(this HttpResponse rsp,
                                                            object? routeValues = null,
                                                            object? responseBody = null,
                                                            Http? verb = null,
                                                            int? routeNumber = null,
                                                            JsonSerializerContext? jsonSerializerContext = null,
                                                            bool generateAbsoluteUrl = false,
                                                            CancellationToken cancellation = default) where TEndpoint : IEndpoint
        => SendAcceptedAtAsync(
            rsp,
            EpOpts.NameGenerator(new(typeof(TEndpoint), verb?.ToString("F"), routeNumber, null)),
            routeValues,
            responseBody,
            jsonSerializerContext,
            generateAbsoluteUrl,
            cancellation);

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
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendAcceptedAtAsync(this HttpResponse rsp,
                                                 string endpointName,
                                                 object? routeValues = null,
                                                 object? responseBody = null,
                                                 JsonSerializerContext? jsonSerializerContext = null,
                                                 bool generateAbsoluteUrl = false,
                                                 CancellationToken cancellation = default)
        => SendAtAsync(rsp, 202, endpointName, routeValues, generateAbsoluteUrl, "application/json", responseBody, jsonSerializerContext, cancellation);

    /// <summary>
    /// send the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendAsync<TResponse>(this HttpResponse rsp,
                                                  TResponse response,
                                                  int statusCode = 200,
                                                  JsonSerializerContext? jsonSerializerContext = null,
                                                  CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.HttpContext.PopulateResponseHeadersFromResponseDto(response);
        rsp.StatusCode = statusCode;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, response);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, response),
            SerOpts.ResponseSerializer(rsp, response, "application/json", jsonSerializerContext, cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a custom 20X (accepted/created) response with a location header containing where the resource can be retrieved from.
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
    /// <param name="statusCode">the http status code to send</param>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="contentType">the content type for the response</param>
    /// <param name="verb">only useful when pointing to a multi verb endpoint</param>
    /// <param name="routeNumber">only useful when pointing to a multi route endpoint</param>
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendAtAsync<TEndpoint>(this HttpResponse rsp,
                                                    int statusCode,
                                                    object? routeValues = null,
                                                    bool generateAbsoluteUrl = false,
                                                    string contentType = "application/json",
                                                    object? responseBody = null,
                                                    Http? verb = null,
                                                    int? routeNumber = null,
                                                    JsonSerializerContext? jsonSerializerContext = null,
                                                    CancellationToken cancellation = default) where TEndpoint : IEndpoint
        => SendAtAsync(
            rsp,
            statusCode,
            EpOpts.NameGenerator(new(typeof(TEndpoint), verb?.ToString("F"), routeNumber, null)),
            routeValues,
            generateAbsoluteUrl,
            contentType,
            responseBody,
            jsonSerializerContext,
            cancellation);

    /// <summary>
    /// send a custom 20X (accepted/created) response with a location header containing where the resource can be retrieved from.
    /// <para>
    /// WARNING: this method is only supported on single verb/route endpoints. it will not produce a `Location` header if used in a multi verb or multi
    /// route endpoint.
    /// </para>
    /// </summary>
    /// <param name="statusCode">the http status code to send</param>
    /// <param name="endpointName">the name of the endpoint to use for link generation (openapi route id)</param>
    /// <param name="routeValues">a route values object with key/value pairs of route information</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="contentType">the content type for the response</param>
    /// <param name="responseBody">the content to be serialized in the response body</param>
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendAtAsync(this HttpResponse rsp,
                                         int statusCode,
                                         string endpointName,
                                         object? routeValues = null,
                                         bool generateAbsoluteUrl = false,
                                         string contentType = "application/json",
                                         object? responseBody = null,
                                         JsonSerializerContext? jsonSerializerContext = null,
                                         CancellationToken cancellation = default)
    {
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        var linkGen = Cfg.ServiceResolver.TryResolve<LinkGenerator>() ??              //unit tests (won't have the LinkGenerator registered)
                      rsp.HttpContext.RequestServices?.GetService<LinkGenerator>() ?? //so get it from httpcontext. do not change to Resolve<T>() here
                      throw new InvalidOperationException("LinkGenerator is not registered! Have you done the unit test setup correctly?");

        rsp.HttpContext.MarkResponseStart();
        rsp.HttpContext.PopulateResponseHeadersFromResponseDto(responseBody);
        rsp.StatusCode = statusCode;
        rsp.Headers.Location = generateAbsoluteUrl
                                   ? linkGen.GetUriByName(rsp.HttpContext, endpointName, routeValues)
                                   : linkGen.GetPathByName(endpointName, routeValues);

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, responseBody);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, responseBody),
            responseBody is null
                ? rsp.StartAsync(cancellation.IfDefault(rsp))
                : SerOpts.ResponseSerializer(rsp, responseBody, contentType, jsonSerializerContext, cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a byte array to the client
    /// </summary>
    /// <param name="bytes">the bytes to send</param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static async Task<Void> SendBytesAsync(this HttpResponse rsp,
                                                  byte[] bytes,
                                                  string? fileName = null,
                                                  string contentType = "application/octet-stream",
                                                  DateTimeOffset? lastModified = null,
                                                  bool enableRangeProcessing = false,
                                                  CancellationToken cancellation = default)
    {
        using var memoryStream = new MemoryStream(bytes);
        await SendStreamAsync(
            rsp,
            memoryStream,
            fileName,
            bytes.Length,
            contentType,
            lastModified,
            enableRangeProcessing,
            cancellation);

        return Void.Instance;
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
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendCreatedAtAsync<TEndpoint>(this HttpResponse rsp,
                                                           object? routeValues = null,
                                                           object? responseBody = null,
                                                           Http? verb = null,
                                                           int? routeNumber = null,
                                                           JsonSerializerContext? jsonSerializerContext = null,
                                                           bool generateAbsoluteUrl = false,
                                                           CancellationToken cancellation = default) where TEndpoint : IEndpoint
        => SendCreatedAtAsync(
            rsp,
            EpOpts.NameGenerator(new(typeof(TEndpoint), verb?.ToString("F"), routeNumber, null)),
            routeValues,
            responseBody,
            jsonSerializerContext,
            generateAbsoluteUrl,
            cancellation);

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
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="generateAbsoluteUrl">set to true for generating an absolute url instead of relative url for the location header</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendCreatedAtAsync(this HttpResponse rsp,
                                                string endpointName,
                                                object? routeValues = null,
                                                object? responseBody = null,
                                                JsonSerializerContext? jsonSerializerContext = null,
                                                bool generateAbsoluteUrl = false,
                                                CancellationToken cancellation = default)
        => SendAtAsync(rsp, 201, endpointName, routeValues, generateAbsoluteUrl, "application/json", responseBody, jsonSerializerContext, cancellation);

    /// <summary>
    /// send an empty json object in the body
    /// </summary>
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendEmptyJsonObject(this HttpResponse rsp,
                                                 JsonSerializerContext? jsonSerializerContext = null,
                                                 CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 200;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            SerOpts.ResponseSerializer(rsp, new JsonObject(), "application/json", jsonSerializerContext, cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a 400 bad request with error details of the current validation failures
    /// </summary>
    /// <param name="failures">the collection of failures</param>
    /// <param name="statusCode">the http status code for the error response</param>
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendErrorsAsync(this HttpResponse rsp,
                                             List<ValidationFailure> failures,
                                             int statusCode = 400,
                                             JsonSerializerContext? jsonSerializerContext = null,
                                             CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = statusCode;

        var content = ErrOpts.ResponseBuilder(failures, rsp.HttpContext, statusCode);

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, content);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, content),
            SerOpts.ResponseSerializer(rsp, content, ErrOpts.ContentType, jsonSerializerContext, cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// start a "server-sent-events" data stream for the client asynchronously without blocking any threads
    /// </summary>
    /// <typeparam name="T">the type of the objects being sent in the event stream</typeparam>
    /// <param name="eventName">the name of the event stream</param>
    /// <param name="eventStream">an IAsyncEnumerable that is the source of the data</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendEventStreamAsync<T>(this HttpResponse rsp,
                                                     string eventName,
                                                     IAsyncEnumerable<T> eventStream,
                                                     CancellationToken cancellation = default) where T : notnull
    {
        return SendEventStreamAsync(rsp, GetStreamItemAsyncEnumerable(eventName, eventStream, cancellation), cancellation);

        static async IAsyncEnumerable<StreamItem> GetStreamItemAsyncEnumerable(string eventName,
                                                                               IAsyncEnumerable<T> source,
                                                                               [EnumeratorCancellation] CancellationToken ct)
        {
            long id = 1;

            await foreach (var item in source.WithCancellation(ct))
                yield return new((id++).ToString(), eventName, item);
        }
    }

    /// <summary>
    /// start a "server-sent-events" data stream for the client asynchronously without blocking any threads
    /// </summary>
    /// <param name="eventStream">an IAsyncEnumerable that is the source of the data</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static async Task<Void> SendEventStreamAsync(this HttpResponse rsp, IAsyncEnumerable<StreamItem> eventStream, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 200;
        rsp.ContentType = "text/event-stream; charset=utf-8";
        rsp.Headers.CacheControl = "no-cache";
        rsp.Headers.Connection = "keep-alive";
        rsp.Headers.Append("X-Accel-Buffering", "no");

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);
        if (EpOpts.GlobalResponseModifierAsync is not null)
            await EpOpts.GlobalResponseModifierAsync.Invoke(rsp.HttpContext, null);

        var ct = cancellation.IfDefault(rsp);
        await rsp.Body.FlushAsync(ct);

        var applicationStopping = rsp.HttpContext.RequestServices.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;

        try
        {
            // Pass the ApplicationStopping CancellationToken to the IAsyncEnumerable, the framework will combine it automatically with any user provided token.
            // This makes sure that the stream at least stops when the host is shutting down.
            await foreach (var streamItem in eventStream.WithCancellation(applicationStopping))
            {
                await rsp.WriteAsync(
                    $"id: {streamItem.Id}\nevent: {streamItem.EventName}\ndata: {streamItem.GetDataString(SerOpts.Options)}\nretry: {streamItem.Retry}\n\n",
                    Encoding.UTF8,
                    ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            // Flush the buffer only if the client did not trigger the cancellation
            if (!ct.IsCancellationRequested)
                await rsp.Body.FlushAsync(ct);
        }

        return Void.Instance;
    }

    /// <summary>
    /// send a file to the client
    /// </summary>
    /// <param name="fileInfo"></param>
    /// <param name="contentType">optional content type to set on the http response</param>
    /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
    /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendFileAsync(this HttpResponse rsp,
                                           FileInfo fileInfo,
                                           string contentType = "application/octet-stream",
                                           DateTimeOffset? lastModified = null,
                                           bool enableRangeProcessing = false,
                                           CancellationToken cancellation = default)
        => SendStreamAsync(
            rsp,
            fileInfo.OpenRead(),
            fileInfo.Name,
            fileInfo.Length,
            contentType,
            lastModified,
            enableRangeProcessing,
            cancellation);

    /// <summary>
    /// send a 403 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendForbiddenAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 403;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send headers in response to a HEAD request
    /// </summary>
    /// <param name="headers">an action to be performed on the headers dictionary of the response</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendHeadersAsync(this HttpResponse rsp,
                                              Action<IHeaderDictionary> headers,
                                              int statusCode = 200,
                                              CancellationToken cancellation = default)
    {
        headers(rsp.Headers);
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = statusCode;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a 204 no content response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendNoContentAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 204;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a 404 not found response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendNotFoundAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 404;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send an http 200 ok response without a body.
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendOkAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 200;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send an http 200 ok response with the supplied response dto serialized as json to the client.
    /// </summary>
    /// <param name="response">the object to serialize to json</param>
    /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendOkAsync<TResponse>(this HttpResponse rsp,
                                                    TResponse response,
                                                    JsonSerializerContext? jsonSerializerContext = null,
                                                    CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.HttpContext.PopulateResponseHeadersFromResponseDto(response);
        rsp.StatusCode = 200;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, response);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, response),
            SerOpts.ResponseSerializer(rsp, response, "application/json", jsonSerializerContext, cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a 302/301 redirect response
    /// </summary>
    /// <param name="location">the location to redirect to</param>
    /// <param name="isPermanent">set to true for a 301 redirect. 302 is the default.</param>
    /// <param name="allowRemoteRedirects">set to true if it's ok to redirect to remote addresses, which is prone to open redirect attacks.</param>
    /// <exception cref="InvalidOperationException">thrown if <paramref name="allowRemoteRedirects" /> is not set to true and the supplied url is not local</exception>
    public static Task<Void> SendRedirectAsync(this HttpResponse rsp, string location, bool isPermanent, bool allowRemoteRedirects = false)
        => SendResultAsync(rsp, allowRemoteRedirects ? Results.Redirect(location, isPermanent) : Results.LocalRedirect(location, isPermanent));

    /// <summary>
    /// execute and send any <see cref="IResult" /> produced by the <see cref="Results" /> or <see cref="TypedResults" /> classes in minimal apis.
    /// </summary>
    /// <param name="result">
    /// the <see cref="IResult" /> instance to execute such as from:
    /// <code>
    ///   - Results.Ok();
    ///   - TypedResults.NotFound();
    /// </code>
    /// </param>
    public static Task<Void> SendResultAsync(this HttpResponse rsp, IResult result)
    {
        rsp.HttpContext.MarkResponseStart();
        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, result);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, result),
            result.ExecuteAsync(rsp.HttpContext));
    }

    /// <summary>
    /// send any http status code
    /// </summary>
    /// <param name="statusCode">the http status code</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendStatusCodeAsync(this HttpResponse rsp, int statusCode, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = statusCode;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
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
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static async Task<Void> SendStreamAsync(this HttpResponse rsp,
                                                   Stream stream,
                                                   string? fileName = null,
                                                   long? fileLengthBytes = null,
                                                   string contentType = "application/octet-stream",
                                                   DateTimeOffset? lastModified = null,
                                                   bool enableRangeProcessing = false,
                                                   CancellationToken cancellation = default)
    {
        if (stream is null)
            throw new ArgumentNullException(nameof(stream), "The supplied stream cannot be null!");

        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 200;

        await using (stream)
        {
            var fileLength = fileLengthBytes;

            if (stream.CanSeek)
                fileLength = stream.Length;

            var (range, rangeLength, shouldSendBody) = StreamHelper.ModifyHeaders(
                rsp.HttpContext,
                contentType,
                fileName,
                fileLength,
                enableRangeProcessing,
                lastModified);

            EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, stream);

            if (!shouldSendBody)
                return Void.Instance;

            await StreamHelper.WriteFileAsync(
                rsp.HttpContext,
                stream,
                range,
                rangeLength,
                cancellation.IfDefault(rsp));

            return Void.Instance;
        }
    }

    /// <summary>
    /// send the supplied string content to the client.
    /// </summary>
    /// <param name="content">the string to write to the response body</param>
    /// <param name="statusCode">optional custom http status code</param>
    /// <param name="contentType">optional content type header value</param>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendStringAsync(this HttpResponse rsp,
                                             string content,
                                             int statusCode = 200,
                                             string contentType = "text/plain; charset=utf-8",
                                             CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = statusCode;
        rsp.ContentType = contentType;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, content);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, content),
            rsp.WriteAsync(content, cancellation.IfDefault(rsp)));
    }

    /// <summary>
    /// send a 401 unauthorized response
    /// </summary>
    /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
    public static Task<Void> SendUnauthorizedAsync(this HttpResponse rsp, CancellationToken cancellation = default)
    {
        rsp.HttpContext.MarkResponseStart();
        rsp.StatusCode = 401;

        EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);

        return Execute(
            EpOpts.GlobalResponseModifierAsync?.Invoke(rsp.HttpContext, null),
            rsp.StartAsync(cancellation.IfDefault(rsp)));
    }

    static CancellationToken IfDefault(this CancellationToken token, HttpResponse httpResponse)
        => token == CancellationToken.None
               ? httpResponse.HttpContext.RequestAborted
               : token;

    static async Task<Void> Execute(Task? modifierTask, Task finalTask)
    {
        if (modifierTask is not null)
            await modifierTask;

        await finalTask.ConfigureAwait(false);

        return Void.Instance;
    }
}