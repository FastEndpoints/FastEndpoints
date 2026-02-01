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

#pragma warning disable IL2026

namespace FastEndpoints;

public static class HttpResponseExtensions
{
    extension(HttpResponse rsp)
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
        public Task<Void> SendAcceptedAtAsync<TEndpoint>(object? routeValues = null,
                                                         object? responseBody = null,
                                                         Http? verb = null,
                                                         int? routeNumber = null,
                                                         JsonSerializerContext? jsonSerializerContext = null,
                                                         bool generateAbsoluteUrl = false,
                                                         CancellationToken cancellation = default) where TEndpoint : IEndpoint
            => rsp.SendAcceptedAtAsync(
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
        public Task<Void> SendAcceptedAtAsync(string endpointName,
                                              object? routeValues = null,
                                              object? responseBody = null,
                                              JsonSerializerContext? jsonSerializerContext = null,
                                              bool generateAbsoluteUrl = false,
                                              CancellationToken cancellation = default)
            => rsp.SendAtAsync(202, endpointName, routeValues, generateAbsoluteUrl, "application/json", responseBody, jsonSerializerContext, cancellation);

        /// <summary>
        /// send the supplied response dto serialized as json to the client.
        /// </summary>
        /// <param name="response">the object to serialize to json</param>
        /// <param name="statusCode">optional custom http status code</param>
        /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendAsync<TResponse>(TResponse response,
                                                     int statusCode = 200,
                                                     JsonSerializerContext? jsonSerializerContext = null,
                                                     CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.HttpContext.StoreResponse(response);
            rsp.HttpContext.PopulateResponseHeadersFromResponseDto(response);
            rsp.StatusCode = statusCode;
            await rsp.ApplyGlobalResponseModifier();
            await SerOpts.ResponseSerializer(rsp, response, "application/json", jsonSerializerContext, cancellation.IfDefault(rsp));

            return Void.Instance;
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
        public Task<Void> SendAtAsync<TEndpoint>(int statusCode,
                                                 object? routeValues = null,
                                                 bool generateAbsoluteUrl = false,
                                                 string contentType = "application/json",
                                                 object? responseBody = null,
                                                 Http? verb = null,
                                                 int? routeNumber = null,
                                                 JsonSerializerContext? jsonSerializerContext = null,
                                                 CancellationToken cancellation = default) where TEndpoint : IEndpoint
            => rsp.SendAtAsync(
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
        public async Task<Void> SendAtAsync(int statusCode,
                                            string endpointName,
                                            object? routeValues = null,
                                            bool generateAbsoluteUrl = false,
                                            string contentType = "application/json",
                                            object? responseBody = null,
                                            JsonSerializerContext? jsonSerializerContext = null,
                                            CancellationToken cancellation = default)
        {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            var linkGen = ServiceResolver.Instance.TryResolve<LinkGenerator>() ??         //unit tests (won't have the LinkGenerator registered)
                          rsp.HttpContext.RequestServices?.GetService<LinkGenerator>() ?? //so get it from httpcontext. do not change to Resolve<T>() here
                          throw new InvalidOperationException("LinkGenerator is not registered! Have you done the unit test setup correctly?");

            rsp.HttpContext.MarkResponseStart();
            rsp.HttpContext.StoreResponse(responseBody);
            rsp.HttpContext.PopulateResponseHeadersFromResponseDto(responseBody);
            rsp.StatusCode = statusCode;
            rsp.Headers.Location = generateAbsoluteUrl
                                       ? linkGen.GetUriByName(rsp.HttpContext, endpointName, routeValues)
                                       : linkGen.GetPathByName(endpointName, routeValues);
            await rsp.ApplyGlobalResponseModifier();
            await (responseBody is null
                       ? rsp.StartAsync(cancellation.IfDefault(rsp))
                       : SerOpts.ResponseSerializer(rsp, responseBody, contentType, jsonSerializerContext, cancellation.IfDefault(rsp)));

            return Void.Instance;
        }

        /// <summary>
        /// send a byte array to the client
        /// </summary>
        /// <param name="bytes">the bytes to send</param>
        /// <param name="contentType">optional content type to set on the http response</param>
        /// <param name="lastModified">optional last modified date-time-offset for the data stream</param>
        /// <param name="enableRangeProcessing">optional switch for enabling range processing</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendBytesAsync(byte[] bytes,
                                               string? fileName = null,
                                               string contentType = "application/octet-stream",
                                               DateTimeOffset? lastModified = null,
                                               bool enableRangeProcessing = false,
                                               CancellationToken cancellation = default)
        {
            using var memoryStream = new MemoryStream(bytes);
            await rsp.SendStreamAsync(
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
        public Task<Void> SendCreatedAtAsync<TEndpoint>(object? routeValues = null,
                                                        object? responseBody = null,
                                                        Http? verb = null,
                                                        int? routeNumber = null,
                                                        JsonSerializerContext? jsonSerializerContext = null,
                                                        bool generateAbsoluteUrl = false,
                                                        CancellationToken cancellation = default) where TEndpoint : IEndpoint
            => rsp.SendCreatedAtAsync(
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
        public Task<Void> SendCreatedAtAsync(string endpointName,
                                             object? routeValues = null,
                                             object? responseBody = null,
                                             JsonSerializerContext? jsonSerializerContext = null,
                                             bool generateAbsoluteUrl = false,
                                             CancellationToken cancellation = default)
            => rsp.SendAtAsync(201, endpointName, routeValues, generateAbsoluteUrl, "application/json", responseBody, jsonSerializerContext, cancellation);

        /// <summary>
        /// send an empty json object in the body
        /// </summary>
        /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendEmptyJsonObject(JsonSerializerContext? jsonSerializerContext = null,
                                                    CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 200;
            await rsp.ApplyGlobalResponseModifier();
            await SerOpts.ResponseSerializer(rsp, new JsonObject(), "application/json", jsonSerializerContext, cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send a 400 bad request with error details of the current validation failures
        /// </summary>
        /// <param name="failures">the collection of failures</param>
        /// <param name="statusCode">the http status code for the error response</param>
        /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendErrorsAsync(List<ValidationFailure> failures,
                                                int statusCode = 400,
                                                JsonSerializerContext? jsonSerializerContext = null,
                                                CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = statusCode;

            var content = ErrOpts.ResponseBuilder(failures, rsp.HttpContext, statusCode);
            rsp.HttpContext.StoreResponse(content);
            await rsp.ApplyGlobalResponseModifier();
            await SerOpts.ResponseSerializer(rsp, content, ErrOpts.ContentType, jsonSerializerContext, cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// start a "server-sent-events" data stream for the client asynchronously without blocking any threads
        /// </summary>
        /// <typeparam name="T">the type of the objects being sent in the event stream</typeparam>
        /// <param name="eventName">the name of the event stream</param>
        /// <param name="eventStream">an IAsyncEnumerable that is the source of the data</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public Task<Void> SendEventStreamAsync<T>(string eventName,
                                                  IAsyncEnumerable<T> eventStream,
                                                  CancellationToken cancellation = default) where T : notnull
        {
            return rsp.SendEventStreamAsync(GetStreamItemAsyncEnumerable(eventName, eventStream, cancellation), cancellation);

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
        public async Task<Void> SendEventStreamAsync(IAsyncEnumerable<StreamItem> eventStream, CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 200;
            rsp.ContentType = "text/event-stream; charset=utf-8";
            rsp.Headers.CacheControl = "no-cache";
            rsp.Headers.Connection = "keep-alive";
            rsp.Headers.Append("X-Accel-Buffering", "no");
            await rsp.ApplyGlobalResponseModifier();

            var ct = cancellation.IfDefault(rsp);
            await rsp.Body.FlushAsync(ct);

            // The C# compiler automatically creates a linked CancellationToken by combining the applicationStopping token passed into the GetAsyncEnumerator method
            // and any token marked by [EnumeratorCancellation] in the method returning the IAsyncEnumerable
            var applicationStopping = rsp.HttpContext.RequestServices.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping;
            await using var enumerator = eventStream.GetAsyncEnumerator(applicationStopping);

            try
            {
                var next = enumerator.MoveNextAsync();

                while (await next)
                {
                    await rsp.WriteAsync(
                        $"id: {enumerator.Current.Id}\nevent: {enumerator.Current.EventName}\ndata: {enumerator.Current.GetDataString(SerOpts.Options)}\nretry: {enumerator.Current.Retry}\n\n",
                        Encoding.UTF8,
                        ct);

                    next = enumerator.MoveNextAsync();

                    if (!next.IsCompleted)
                    {
                        // flush the data to the client if the next item is not already available
                        await rsp.Body.FlushAsync(ct);
                    }
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
        public Task<Void> SendFileAsync(FileInfo fileInfo,
                                        string contentType = "application/octet-stream",
                                        DateTimeOffset? lastModified = null,
                                        bool enableRangeProcessing = false,
                                        CancellationToken cancellation = default)
            => rsp.SendStreamAsync(
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
        public async Task<Void> SendForbiddenAsync(CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 403;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send headers in response to a HEAD request
        /// </summary>
        /// <param name="headers">an action to be performed on the headers dictionary of the response</param>
        /// <param name="statusCode">optional custom http status code</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendHeadersAsync(Action<IHeaderDictionary> headers,
                                                 int statusCode = 200,
                                                 CancellationToken cancellation = default)
        {
            headers(rsp.Headers);
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = statusCode;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send a 204 no content response
        /// </summary>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendNoContentAsync(CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 204;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send a 304 not modified response
        /// </summary>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendNotModifiedAsync(CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 304;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send a 404 not found response
        /// </summary>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendNotFoundAsync(CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 404;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send an http 200 ok response without a body.
        /// </summary>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendOkAsync(CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 200;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send an http 200 ok response with the supplied response dto serialized as json to the client.
        /// </summary>
        /// <param name="response">the object to serialize to json</param>
        /// <param name="jsonSerializerContext">json serializer context if code generation is used</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendOkAsync<TResponse>(TResponse response,
                                                       JsonSerializerContext? jsonSerializerContext = null,
                                                       CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.HttpContext.StoreResponse(response);
            rsp.HttpContext.PopulateResponseHeadersFromResponseDto(response);
            rsp.StatusCode = 200;
            await rsp.ApplyGlobalResponseModifier();
            await SerOpts.ResponseSerializer(rsp, response, "application/json", jsonSerializerContext, cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send a 302/301 redirect response
        /// </summary>
        /// <param name="location">the location to redirect to</param>
        /// <param name="isPermanent">set to true for a 301 redirect. 302 is the default.</param>
        /// <param name="allowRemoteRedirects">set to true if it's ok to redirect to remote addresses, which is prone to open redirect attacks.</param>
        /// <exception cref="InvalidOperationException">thrown if <paramref name="allowRemoteRedirects" /> is not set to true and the supplied url is not local</exception>
        public Task<Void> SendRedirectAsync(string location, bool isPermanent, bool allowRemoteRedirects = false)
            => rsp.SendResultAsync(allowRemoteRedirects ? Results.Redirect(location, isPermanent) : Results.LocalRedirect(location, isPermanent));

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
        public async Task<Void> SendResultAsync(IResult result)
        {
            rsp.HttpContext.MarkResponseStart();
            await rsp.ApplyGlobalResponseModifier();
            await result.ExecuteAsync(rsp.HttpContext);

            return Void.Instance;
        }

        /// <summary>
        /// send any http status code
        /// </summary>
        /// <param name="statusCode">the http status code</param>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendStatusCodeAsync(int statusCode, CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = statusCode;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
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
        public async Task<Void> SendStreamAsync(Stream stream,
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

                await rsp.ApplyGlobalResponseModifier();

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
        public async Task<Void> SendStringAsync(string content,
                                                int statusCode = 200,
                                                string contentType = "text/plain; charset=utf-8",
                                                CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = statusCode;
            rsp.ContentType = contentType;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.WriteAsync(content, cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        /// <summary>
        /// send a 401 unauthorized response
        /// </summary>
        /// <param name="cancellation">optional cancellation token. if not specified, the <c>HttpContext.RequestAborted</c> token is used.</param>
        public async Task<Void> SendUnauthorizedAsync(CancellationToken cancellation = default)
        {
            rsp.HttpContext.MarkResponseStart();
            rsp.StatusCode = 401;
            await rsp.ApplyGlobalResponseModifier();
            await rsp.StartAsync(cancellation.IfDefault(rsp));

            return Void.Instance;
        }

        async Task ApplyGlobalResponseModifier()
        {
            EpOpts.GlobalResponseModifier?.Invoke(rsp.HttpContext, null);
            if (EpOpts.GlobalResponseModifierAsync is not null)
                await EpOpts.GlobalResponseModifierAsync(rsp.HttpContext, null);
        }
    }

    static CancellationToken IfDefault(this CancellationToken token, HttpResponse httpResponse)
        => token == CancellationToken.None
               ? httpResponse.HttpContext.RequestAborted
               : token;
}