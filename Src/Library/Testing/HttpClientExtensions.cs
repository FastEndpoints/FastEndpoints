// ReSharper disable InconsistentNaming

using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// a set of extensions to the httpclient in order to facilitate route-less integration testing
/// </summary>
[UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050"), UnconditionalSuppressMessage("aot", "IL2075")]
public static class HttpClientExtensions
{
    static readonly JsonSerializerOptions _errSerOpts = new(SerOpts.Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    extension(HttpClient client)
    {
        /// <summary>
        /// make a POST request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as well
        /// as the <typeparamref name="TResponse" /> DTO/>.
        /// </summary>
        /// <typeparam name="TRequest">type of the request dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="requestUri">the route url to post to</param>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> POSTAsync<TRequest, TResponse>(string requestUri,
                                                                          TRequest request,
                                                                          bool sendAsFormData = false,
                                                                          bool populateHeaders = true,
                                                                          bool populateCookies = true) where TRequest : notnull
            => client.SENDAsync<TRequest, TResponse>(HttpMethod.Post, requestUri, request, sendAsFormData, populateHeaders, populateCookies);

        /// <summary>
        /// make a POST request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
        /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> POSTAsync<TEndpoint, TRequest, TResponse>(TRequest request,
                                                                                     bool sendAsFormData = false,
                                                                                     bool populateHeaders = true,
                                                                                     bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
            => client.POSTAsync<TRequest, TResponse>(
                GetTestUrlFor<TEndpoint, TRequest>(request, client),
                request,
                sendAsFormData,
                populateHeaders,
                populateCookies);

        /// <summary>
        /// make a POST request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public async Task<HttpResponseMessage> POSTAsync<TEndpoint, TRequest>(TRequest request,
                                                                              bool sendAsFormData = false,
                                                                              bool populateHeaders = true,
                                                                              bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
        {
            var (rsp, _) = await client.POSTAsync<TEndpoint, TRequest, EmptyResponse>(request, sendAsFormData, populateHeaders, populateCookies);

            return rsp;
        }

        /// <summary>
        /// make a POST request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
        /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public Task<TestResult<TResponse>> POSTAsync<TEndpoint, TResponse>() where TEndpoint : IEndpoint
            => client.POSTAsync<TEndpoint, EmptyRequest, TResponse>(EmptyRequest.Instance);

        /// <summary>
        /// make a PATCH request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as
        /// well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TRequest">type of the request dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="requestUri">the route url to PATCH to</param>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> PATCHAsync<TRequest, TResponse>(string requestUri,
                                                                           TRequest request,
                                                                           bool sendAsFormData = false,
                                                                           bool populateHeaders = true,
                                                                           bool populateCookies = true) where TRequest : notnull
            => client.SENDAsync<TRequest, TResponse>(HttpMethod.Patch, requestUri, request, sendAsFormData, populateHeaders, populateCookies);

        /// <summary>
        /// make a PATCH request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
        /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
        /// </param>
        public Task<TestResult<TResponse>> PATCHAsync<TEndpoint, TRequest, TResponse>(TRequest request,
                                                                                      bool sendAsFormData = false,
                                                                                      bool populateHeaders = true,
                                                                                      bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
            => client.PATCHAsync<TRequest, TResponse>(
                GetTestUrlFor<TEndpoint, TRequest>(request, client),
                request,
                sendAsFormData,
                populateHeaders,
                populateCookies);

        /// <summary>
        /// make a PATCH request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public async Task<HttpResponseMessage> PATCHAsync<TEndpoint, TRequest>(TRequest request,
                                                                               bool sendAsFormData = false,
                                                                               bool populateHeaders = true,
                                                                               bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
        {
            var (rsp, _) = await client.PATCHAsync<TEndpoint, TRequest, EmptyResponse>(request, sendAsFormData, populateHeaders, populateCookies);

            return rsp;
        }

        /// <summary>
        /// make a PATCH request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
        /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public Task<TestResult<TResponse>> PATCHAsync<TEndpoint, TResponse>() where TEndpoint : IEndpoint
            => client.PATCHAsync<TEndpoint, EmptyRequest, TResponse>(EmptyRequest.Instance);

        /// <summary>
        /// make a PUT request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as well
        /// as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TRequest">type of the request dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="requestUri">the route url to post to</param>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> PUTAsync<TRequest, TResponse>(string requestUri,
                                                                         TRequest request,
                                                                         bool sendAsFormData = false,
                                                                         bool populateHeaders = true,
                                                                         bool populateCookies = true) where TRequest : notnull
            => client.SENDAsync<TRequest, TResponse>(HttpMethod.Put, requestUri, request, sendAsFormData, populateHeaders, populateCookies);

        /// <summary>
        /// make a PUT request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
        /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> PUTAsync<TEndpoint, TRequest, TResponse>(TRequest request,
                                                                                    bool sendAsFormData = false,
                                                                                    bool populateHeaders = true,
                                                                                    bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
            => client.PUTAsync<TRequest, TResponse>(GetTestUrlFor<TEndpoint, TRequest>(request, client), request, sendAsFormData, populateHeaders, populateCookies);

        /// <summary>
        /// make a PUT request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public async Task<HttpResponseMessage> PUTAsync<TEndpoint, TRequest>(TRequest request,
                                                                             bool sendAsFormData = false,
                                                                             bool populateHeaders = true,
                                                                             bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
        {
            var (rsp, _) = await client.PUTAsync<TEndpoint, TRequest, EmptyResponse>(request, sendAsFormData, populateHeaders, populateCookies);

            return rsp;
        }

        /// <summary>
        /// make a PUT request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
        /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public Task<TestResult<TResponse>> PUTAsync<TEndpoint, TResponse>() where TEndpoint : IEndpoint
            => client.PUTAsync<TEndpoint, EmptyRequest, TResponse>(EmptyRequest.Instance);

        /// <summary>
        /// make a GET request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as well
        /// as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TRequest">type of the request dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="requestUri">the route url to post to</param>
        /// <param name="request">the request dto</param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> GETAsync<TRequest, TResponse>(string requestUri,
                                                                         TRequest request,
                                                                         bool populateHeaders = true,
                                                                         bool populateCookies = true) where TRequest : notnull
            => client.SENDAsync<TRequest, TResponse>(HttpMethod.Get, requestUri, request, populateHeaders: populateHeaders, populateCookies: populateCookies);

        /// <summary>
        /// make a GET request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
        /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> GETAsync<TEndpoint, TRequest, TResponse>(TRequest request,
                                                                                    bool populateHeaders = true,
                                                                                    bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
            => client.GETAsync<TRequest, TResponse>(GetTestUrlFor<TEndpoint, TRequest>(request, client), request, populateHeaders, populateCookies);

        /// <summary>
        /// make a GET request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public async Task<HttpResponseMessage> GETAsync<TEndpoint, TRequest>(TRequest request,
                                                                             bool populateHeaders = true,
                                                                             bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
        {
            var (rsp, _) = await client.GETAsync<TEndpoint, TRequest, EmptyResponse>(request, populateHeaders, populateCookies);

            return rsp;
        }

        /// <summary>
        /// make a GET request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
        /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public Task<TestResult<TResponse>> GETAsync<TEndpoint, TResponse>() where TEndpoint : IEndpoint
            => client.GETAsync<TEndpoint, EmptyRequest, TResponse>(EmptyRequest.Instance);

        /// <summary>
        /// make a DELETE request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as
        /// well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TRequest">type of the request dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="requestUri">the route url to post to</param>
        /// <param name="request">the request dto</param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> DELETEAsync<TRequest, TResponse>(string requestUri,
                                                                            TRequest request,
                                                                            bool populateHeaders = true,
                                                                            bool populateCookies = true) where TRequest : notnull
            => client.SENDAsync<TRequest, TResponse>(HttpMethod.Delete, requestUri, request, populateHeaders: populateHeaders, populateCookies: populateCookies);

        /// <summary>
        /// make a DELETE request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing
        /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public Task<TestResult<TResponse>> DELETEAsync<TEndpoint, TRequest, TResponse>(TRequest request,
                                                                                       bool populateHeaders = true,
                                                                                       bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
            => client.DELETEAsync<TRequest, TResponse>(GetTestUrlFor<TEndpoint, TRequest>(request, client), request, populateHeaders, populateCookies);

        /// <summary>
        /// make a DELETE request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TRequest">the type of the request dto</typeparam>
        /// <param name="request">the request dto</param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public async Task<HttpResponseMessage> DELETEAsync<TEndpoint, TRequest>(TRequest request,
                                                                                bool populateHeaders = true,
                                                                                bool populateCookies = true)
            where TEndpoint : IEndpoint where TRequest : notnull
        {
            var (rsp, _) = await client.DELETEAsync<TEndpoint, TRequest, EmptyResponse>(request, populateHeaders, populateCookies);

            return rsp;
        }

        /// <summary>
        /// make a DELETE request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
        /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public Task<TestResult<TResponse>> DELETEAsync<TEndpoint, TResponse>() where TEndpoint : IEndpoint
            => client.DELETEAsync<TEndpoint, EmptyRequest, TResponse>(EmptyRequest.Instance);

        /// <summary>
        /// send a request DTO to a given endpoint URL and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as
        /// well as the <typeparamref name="TResponse" /> DTO
        /// </summary>
        /// <typeparam name="TRequest">type of the request dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="method">the http method to use</param>
        /// <param name="requestUri">the route url of the endpoint</param>
        /// <param name="request">the request dto</param>
        /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
        /// <param name="populateHeaders">
        /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the
        /// [FromHeader] attribute.
        /// </param>
        /// <param name="populateCookies">
        /// when set to false, cookies will not be automatically added to the http request from request dto properties decorated with the
        /// [FromCookie] attribute.
        /// </param>
        public async Task<TestResult<TResponse>> SENDAsync<TRequest, TResponse>(HttpMethod method,
                                                                                string requestUri,
                                                                                TRequest request,
                                                                                bool sendAsFormData = false,
                                                                                bool populateHeaders = true,
                                                                                bool populateCookies = true) where TRequest : notnull
        {
            var msg = new HttpRequestMessage
            {
                Method = method,
                RequestUri = new($"{client.BaseAddress}{requestUri.TrimStart('/')}"),
                Content = sendAsFormData ? request.ToForm() : request.ToContent()
            };
            msg.Headers.Add(Constants.RoutelessTest, "true");

            if (populateHeaders)
                PopulateHeaders(msg, request);

            if (populateCookies)
                PopulateCookies(msg, request);

            var rsp = await client.SendAsync(msg);

            if (typeof(TResponse) == Types.EmptyResponse)
                return new(rsp, default!);

            await rsp.Content.LoadIntoBufferAsync();            // enables consuming the response stream repeatedly.
            var stream = await rsp.Content.ReadAsStreamAsync(); // do not dispose this stream. HttpContent is the owner which handles disposing it.

            if (rsp.Content.Headers.ContentType?.MediaType?.Contains("json") is true)
            {
                try
                {
                    return new(rsp, JsonSerializer.Deserialize<TResponse>(stream, _errSerOpts)!);
                }
                catch
                {
                    return new(
                        rsp,
                        default!,
                        $"Unable to deserialize response body to DTO type:" +
                        $" [{typeof(TResponse).FullName}]. \n\nReceived JSON: \n\n{await rsp.Content.ReadAsStringAsync()}\n");
                }
                finally
                {
                    stream.Position = 0;
                }
            }

            if (rsp.IsSuccessStatusCode)
                return new(rsp, default!);

            try
            {
                return new(rsp, default!, await rsp.Content.ReadAsStringAsync());
            }
            finally
            {
                stream.Position = 0;
            }
        }
    }

    static StringContent? ToContent<TRequest>(this TRequest request) where TRequest : notnull
    {
        foreach (var prop in request.GetType().BindableProps())
        {
            if (prop.GetCustomAttribute<FromFormAttribute>() is not null)
                continue;
            if (prop.GetCustomAttribute<FromClaimAttribute>()?.IsRequired is true)
                continue;
            if (prop.GetCustomAttribute<FromHeaderAttribute>()?.IsRequired is true)
                continue;
            if (prop.GetCustomAttribute<HasPermissionAttribute>()?.IsRequired is true)
                continue;
            if (prop.GetCustomAttribute<FromCookieAttribute>()?.IsRequired is true)
                continue;
            if (prop.GetCustomAttribute<DontBindAttribute>(true) is not null) //covers FormField, RouteParam, QueryParam, FromQuery
                continue;

            return new(JsonSerializer.Serialize(request, SerOpts.Options), Encoding.UTF8, "application/json");
        }

        return null;
    }

    static readonly string[] contentHeaders =
    [
        "Content-Encoding",
        "Content-Language",
        "Content-Length",
        "Content-Location",
        "Content-MD5",
        "Content-Range",
        "Content-Type"
    ];

    static void PopulateHeaders<TRequest>(HttpRequestMessage reqMsg, TRequest req) where TRequest : notnull
    {
        var hdrProps = req.GetType()
                          .BindableProps()
                          .Where(p => p.GetCustomAttribute<FromHeaderAttribute>()?.IsRequired is true);

        foreach (var prop in hdrProps)
        {
            var headerName = prop.GetCustomAttribute<FromHeaderAttribute>()?.HeaderName ?? prop.FieldName();

            if (contentHeaders.Contains(headerName, StringComparer.OrdinalIgnoreCase))
                continue;

            var headerValue = prop.GetValueAsString(req);

            if (headerValue is not null)
                reqMsg.Headers.Add(headerName, headerValue);
        }
    }

    static void PopulateCookies<TRequest>(HttpRequestMessage reqMsg, TRequest req) where TRequest : notnull
    {
        if (reqMsg.RequestUri is null)
            return;

        var cookieProps = req.GetType()
                             .BindableProps()
                             .Where(p => p.GetCustomAttribute<FromCookieAttribute>()?.IsRequired is true);

        var cookieJar = new CookieContainer();

        foreach (var prop in cookieProps)
        {
            var cookieName = prop.GetCustomAttribute<FromCookieAttribute>()?.CookieName ?? prop.FieldName();
            var cookieValue = prop.GetValueAsString(req);

            cookieJar.Add(new Cookie(cookieName, cookieValue, "/", reqMsg.RequestUri.Host));
        }

        if (cookieJar.Count == 0)
            return;

        reqMsg.Headers.Add("Cookie", cookieJar.GetCookieHeader(reqMsg.RequestUri));
    }

    static readonly ConcurrentDictionary<string, string> _testUrlCache = new();

    internal static string GetTestUrlFor<TEndpoint, TRequest>(TRequest req, HttpClient client) where TRequest : notnull
    {
        var epTypeName = typeof(TEndpoint).FullName ?? throw new InvalidOperationException("Unable to determine endpoint type name!");

        if (!_testUrlCache.ContainsKey(epTypeName))
        {
            bool shouldGetViaHttp;
            string? url = null;

            try
            {
                url = IEndpoint.TestURLFor<TEndpoint>();
                shouldGetViaHttp = false;
            }
            catch (KeyNotFoundException) //will be thrown when running with aspire tests (due to aspire black-boxing)
            {
                shouldGetViaHttp = true;
            }

            if (shouldGetViaHttp)
            {
                var res = client.GetFromJsonAsync<string[]>("_test_url_cache_").GetAwaiter().GetResult();

                foreach (var line in res ?? [])
                {
                    var parts = line.Split('|');
                    _testUrlCache.TryAdd(parts[0], parts[1]);
                }
            }
            else
            {
                _testUrlCache.TryAdd(epTypeName, url!);
            }
        }

        // request with multiple repeating dtos, most likely not populated from route values.
        // we don't know which one to populate from anyway.
        if (req is IEnumerable)
            return _testUrlCache[epTypeName];

        //get props and stick em in a dictionary for easy lookup
        //ignore props annotated with security/header/cookie attributes that has IsRequired set to true.
        var reqProps = req.GetType()
                          .BindableProps()
                          .Where(
                              p => p.GetCustomAttribute<FromClaimAttribute>()?.IsRequired is not true &&
                                   p.GetCustomAttribute<FromHeaderAttribute>()?.IsRequired is not true &&
                                   p.GetCustomAttribute<HasPermissionAttribute>()?.IsRequired is not true &&
                                   p.GetCustomAttribute<FromCookieAttribute>()?.IsRequired is not true)
                          .ToDictionary(p => p.FieldName(), StringComparer.OrdinalIgnoreCase);

        //split url into route segments, iterate and replace param names with values from matching dto props
        //while rebuilding the url back up again into a string builder
        StringBuilder sb = new();
        var routeSegments = _testUrlCache[epTypeName].Split('/', StringSplitOptions.RemoveEmptyEntries); // group root endpoints are allowed to set string.empty #988

        if (routeSegments.Length > 0)
        {
            foreach (var segment in routeSegments)
            {
                if (!segment.StartsWith('{') && !segment.EndsWith('}'))
                {
                    sb.Append(segment).Append('/');

                    continue;
                }

                //examples: {id}, {id?}, {id:int}, {ssn:regex(^\\d{{3}}-\\d{{2}}-\\d{{4}}$)}

                var segmentParts = segment.Split(':', StringSplitOptions.RemoveEmptyEntries);
                var isLastSegment = routeSegments.Last() == segment;
                var isOptional = segment.Contains('?');
                var propName = (segmentParts.Length == 1
                                    ? segmentParts[0][1..^1]
                                    : segmentParts[0][1..]).TrimEnd('?');

                var propVal = reqProps.TryGetValue(propName, out var prop)
                                  ? prop.GetValueAsString(req)
                                  : segment;

                if (propVal is null)
                {
                    switch (isOptional)
                    {
                        case true when isLastSegment:
                            continue;
                        case true when !isLastSegment:
                            throw new InvalidOperationException($"Optional route parameter [{segment}] must be the last route segment.");
                        case false:
                            throw new InvalidOperationException($"Route param value missing for required param [{segment}].");
                    }
                }

                sb.Append(propVal);
                sb.Append('/');
            }
            sb.Length--; //remove the last '/'
        }

        //append query parameters if there's any props decorated with [QueryParam]
        var queryParamProps = reqProps.Where(p => p.Value.GetCustomAttribute<DontBindAttribute>()?.BindingSources.HasFlag(Source.QueryParam) is false).ToArray();

        if (queryParamProps.Length > 0)
        {
            var hasAny = false;

            foreach (var qp in queryParamProps)
            {
                var value = qp.Value.GetValueAsString(req);

                if (value is null)
                    continue;

                sb.Append(hasAny ? '&' : '?')
                  .Append(qp.Key)
                  .Append('=')
                  .Append(value);

                hasAny = true;
            }
        }

        return sb.ToString();
    }

    static MultipartFormDataContent ToForm<TRequest>(this TRequest req)
    {
        var form = new MultipartFormDataContent();

        foreach (var p in req!.GetType().BindableProps())
        {
            if (p.PropertyType.GetUnderlyingType() == Types.IFormFile)
                AddFileToForm(p.GetValue(req) as IFormFile, p);

            else if (p.PropertyType.IsAssignableTo(Types.IEnumerableOfIFormFile))
            {
                var files = p.GetValue(req) as IFormFileCollection;

                if (files?.Count is 0 or null)
                    continue;

                foreach (var file in files)
                    AddFileToForm(file, p);
            }
            else
            {
                var value = p.GetValueAsString(req);
                if (value is not null)
                    form.Add(new StringContent(value), p.Name);
            }
        }

        return !form.Any()
                   ? throw new InvalidOperationException("Converting the request DTO to MultipartFormDataContent was unsuccessful!")
                   : form;

        void AddFileToForm(IFormFile? file, PropertyInfo prop)
        {
            if (file is null)
                return;

            var content = new StreamContent(file.OpenReadStream());

            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            if (file.Headers?.ContainsKey(HeaderNames.ContentType) is true)
                content.Headers.ContentType = new(file.ContentType);

            form.Add(content, prop.FieldName(), file.FileName);
        }
    }

    static string? GetValueAsString(this PropertyInfo p, object req)
    {
        var value = p.GetValue(req);

        if (value is null)
            return null;

        var type = p.PropertyType;

        var toStringMethod = type.GetMethod("ToString", Type.EmptyTypes);
        var isRecord = type.GetMethod("<Clone>$") is not null;

        //use overridden ToString() method except for records
        if (toStringMethod is not null && toStringMethod.DeclaringType != Types.Object && !isRecord)
            return ToInvariantIsoString(value);

        try
        {
            var json = JsonSerializer.Serialize(value, SerOpts.Options);

            //this is a json string literal
            if (json.StartsWith('"') && json.EndsWith('"'))
                return json.TrimStart('"').TrimEnd('"');

            //this is either a json array or object
            return json;
        }
        catch
        {
            if (p.IsDefined(Types.FromFormAttribute))
            {
                throw new NotSupportedException(
                    "Automatically constructing MultiPartFormData requests for properties annotated with [FromForm] is not yet supported!");
            }

            throw;
        }

        static string? ToInvariantIsoString(object value)
        {
            return value switch
            {
                DateTime dt => dt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                DateTimeOffset dto => dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TimeOnly t => t.ToString("HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
                TimeSpan ts => ts.ToString("c", CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                Enum e => e.ToString(),
                Guid g => g.ToString("D"),
                float f => f.ToString("R", CultureInfo.InvariantCulture),
                double d => d.ToString("R", CultureInfo.InvariantCulture),
                decimal m => m.ToString(CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                _ => value.ToString()
            };
        }
    }
}