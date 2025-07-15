// ReSharper disable InconsistentNaming

using System.Collections;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// a set of extensions to the httpclient in order to facilitate route-less integration testing
/// </summary>
public static class HttpClientExtensions
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
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> POSTAsync<TRequest, TResponse>(this HttpClient client,
                                                                             string requestUri,
                                                                             TRequest request,
                                                                             bool sendAsFormData = false,
                                                                             bool populateHeaders = true) where TRequest : notnull
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Post, requestUri, request, sendAsFormData, populateHeaders);

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
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> POSTAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                        TRequest request,
                                                                                        bool sendAsFormData = false,
                                                                                        bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
        => POSTAsync<TRequest, TResponse>(client, GetTestUrlFor<TEndpoint, TRequest>(request), request, sendAsFormData, populateHeaders);

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static async Task<HttpResponseMessage> POSTAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                 TRequest request,
                                                                                 bool sendAsFormData = false,
                                                                                 bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
    {
        var (rsp, _) = await POSTAsync<TEndpoint, TRequest, EmptyResponse>(client, request, sendAsFormData, populateHeaders);

        return rsp;
    }

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> POSTAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => POSTAsync<TEndpoint, EmptyRequest, TResponse>(client, new());

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
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> PATCHAsync<TRequest, TResponse>(this HttpClient client,
                                                                              string requestUri,
                                                                              TRequest request,
                                                                              bool sendAsFormData = false,
                                                                              bool populateHeaders = true) where TRequest : notnull
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Patch, requestUri, request, sendAsFormData, populateHeaders);

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
    public static Task<TestResult<TResponse>> PATCHAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                         TRequest request,
                                                                                         bool sendAsFormData = false,
                                                                                         bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
        => PATCHAsync<TRequest, TResponse>(client, GetTestUrlFor<TEndpoint, TRequest>(request), request, sendAsFormData, populateHeaders);

    /// <summary>
    /// make a PATCH request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static async Task<HttpResponseMessage> PATCHAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                  TRequest request,
                                                                                  bool sendAsFormData = false,
                                                                                  bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
    {
        var (rsp, _) = await PATCHAsync<TEndpoint, TRequest, EmptyResponse>(client, request, sendAsFormData, populateHeaders);

        return rsp;
    }

    /// <summary>
    /// make a PATCH request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> PATCHAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => PATCHAsync<TEndpoint, EmptyRequest, TResponse>(client, new());

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
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> PUTAsync<TRequest, TResponse>(this HttpClient client,
                                                                            string requestUri,
                                                                            TRequest request,
                                                                            bool sendAsFormData = false,
                                                                            bool populateHeaders = true) where TRequest : notnull
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Put, requestUri, request, sendAsFormData, populateHeaders);

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
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> PUTAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                       TRequest request,
                                                                                       bool sendAsFormData = false,
                                                                                       bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
        => PUTAsync<TRequest, TResponse>(client, GetTestUrlFor<TEndpoint, TRequest>(request), request, sendAsFormData, populateHeaders);

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static async Task<HttpResponseMessage> PUTAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                TRequest request,
                                                                                bool sendAsFormData = false,
                                                                                bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
    {
        var (rsp, _) = await PUTAsync<TEndpoint, TRequest, EmptyResponse>(client, request, sendAsFormData, populateHeaders);

        return rsp;
    }

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> PUTAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => PUTAsync<TEndpoint, EmptyRequest, TResponse>(client, new());

    /// <summary>
    /// make a GET request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as well
    /// as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> GETAsync<TRequest, TResponse>(this HttpClient client,
                                                                            string requestUri,
                                                                            TRequest request,
                                                                            bool populateHeaders = true) where TRequest : notnull
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Get, requestUri, request, populateHeaders: populateHeaders);

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> GETAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                       TRequest request,
                                                                                       bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
        => GETAsync<TRequest, TResponse>(client, GetTestUrlFor<TEndpoint, TRequest>(request), request, populateHeaders);

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static async Task<HttpResponseMessage> GETAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                TRequest request,
                                                                                bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
    {
        var (rsp, _) = await GETAsync<TEndpoint, TRequest, EmptyResponse>(client, request, populateHeaders);

        return rsp;
    }

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> GETAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => GETAsync<TEndpoint, EmptyRequest, TResponse>(client, new());

    /// <summary>
    /// make a DELETE request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as
    /// well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> DELETEAsync<TRequest, TResponse>(this HttpClient client,
                                                                               string requestUri,
                                                                               TRequest request,
                                                                               bool populateHeaders = true) where TRequest : notnull
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Delete, requestUri, request, populateHeaders: populateHeaders);

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static Task<TestResult<TResponse>> DELETEAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                          TRequest request,
                                                                                          bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
        => DELETEAsync<TRequest, TResponse>(client, GetTestUrlFor<TEndpoint, TRequest>(request), request, populateHeaders);

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="populateHeaders">
    /// when set to true, headers will be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static async Task<HttpResponseMessage> DELETEAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                   TRequest request,
                                                                                   bool populateHeaders = true)
        where TEndpoint : IEndpoint where TRequest : notnull
    {
        var (rsp, _) = await DELETEAsync<TEndpoint, TRequest, EmptyResponse>(client, request, populateHeaders);

        return rsp;
    }

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> DELETEAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => DELETEAsync<TEndpoint, EmptyRequest, TResponse>(client, new());

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
    /// when set to false, headers will not be automatically added to the http request from request dto properties decorated with the [FromHeader] attribute.
    /// </param>
    public static async Task<TestResult<TResponse>> SENDAsync<TRequest, TResponse>(this HttpClient client,
                                                                                   HttpMethod method,
                                                                                   string requestUri,
                                                                                   TRequest request,
                                                                                   bool sendAsFormData = false,
                                                                                   bool populateHeaders = true) where TRequest : notnull
    {
        var msg = new HttpRequestMessage
        {
            Method = method,
            RequestUri = new($"{client.BaseAddress}{requestUri.TrimStart('/')}"),
            Content = sendAsFormData
                          ? request.ToForm()
                          : new StringContent(JsonSerializer.Serialize(request, SerOpts.Options), Encoding.UTF8, "application/json")
        };
        msg.Headers.Add(Constants.RoutelessTest, "true");

        if (populateHeaders)
            PopulateHeaders(msg.Headers, request);

        var rsp = await client.SendAsync(msg);

        var hasNoJsonContent = rsp.Content.Headers.ContentType?.MediaType?.Contains("json") is null or false;
        TResponse? res = default!;

        if (typeof(TResponse) == Types.EmptyResponse || hasNoJsonContent)
            return new(rsp, res);

        if (rsp.IsSuccessStatusCode)
        {
            //this disposes the content stream. test code doesn't need to read it again.
            res = await rsp.Content.ReadFromJsonAsync<TResponse>(SerOpts.Options);
        }
        else
        {
            //make a copy of the content stream to allow test code to read content stream.
            using var copy = new MemoryStream();
            await rsp.Content.CopyToAsync(copy); //this doesn't dispose the original stream.
            copy.Position = 0;

            try
            {
                res = await JsonSerializer.DeserializeAsync<TResponse>(copy, SerOpts.Options);
            }
            catch
            {
                //do nothing
            }
        }

        return new(rsp, res!);
    }

    static void PopulateHeaders<TRequest>(HttpRequestHeaders headers, TRequest req) where TRequest : notnull
    {
        var hdrProps = req.GetType()
                          .BindableProps()
                          .Where(p => p.GetCustomAttribute<FromHeaderAttribute>()?.IsRequired is true);

        foreach (var prop in hdrProps)
            headers.Add(prop.FieldName(), prop.GetValueAsString(req));
    }

    static string GetTestUrlFor<TEndpoint, TRequest>(TRequest req) where TRequest : notnull
    {
        // request with multiple repeating dtos, most likely not populated from route values.
        // we don't know which one to populate from anyway.
        if (req is IEnumerable)
            return IEndpoint.TestURLFor<TEndpoint>();

        //get props and stick em in a dictionary for easy lookup
        //ignore props annotated with security related attributes that has IsRequired set to true.
        var reqProps = req.GetType()
                          .BindableProps()
                          .Where(
                              p => p.GetCustomAttribute<FromClaimAttribute>()?.IsRequired is not true &&
                                   p.GetCustomAttribute<FromHeaderAttribute>()?.IsRequired is not true &&
                                   p.GetCustomAttribute<HasPermissionAttribute>()?.IsRequired is not true)
                          .ToDictionary(p => p.FieldName(), StringComparer.OrdinalIgnoreCase);

        //split url into route segments, iterate and replace param names with values from matching dto props
        //while rebuilding the url back up again into a string builder
        StringBuilder sb = new();
        var routeSegments = IEndpoint.TestURLFor<TEndpoint>().Split('/');

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

        //append query parameters if there's any props decorated with [QueryParam]
        var queryParamProps = reqProps.Where(p => p.Value.GetCustomAttribute<DontBindAttribute>()?.BindingSources.HasFlag(Source.QueryParam) is false).ToArray();

        if (queryParamProps.Length > 0)
        {
            sb.Append('?');

            foreach (var qp in queryParamProps)
                sb.Append(qp.Key).Append('=').Append(qp.Value.GetValueAsString(req)).Append('&');
        }

        return sb.Remove(sb.Length - 1, 1).ToString();
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
        if (toStringMethod is not null && toStringMethod.DeclaringType != Types.Object && isRecord is false)
            return value.ToString();

        var json = JsonSerializer.Serialize(value, SerOpts.Options);

        //this is a json string literal
        if (json.StartsWith('"') && json.EndsWith('"'))
            return json.TrimStart('"').TrimEnd('"');

        //this is either a json array or object
        return json;
    }
}