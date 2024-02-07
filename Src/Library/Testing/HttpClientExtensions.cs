// ReSharper disable InconsistentNaming

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
    public static Task<TestResult<TResponse>> POSTAsync<TRequest, TResponse>(this HttpClient client,
                                                                             string requestUri,
                                                                             TRequest request,
                                                                             bool? sendAsFormData = null)
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Post, requestUri, request, sendAsFormData);

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static Task<TestResult<TResponse>> POSTAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                        TRequest request,
                                                                                        bool? sendAsFormData = null) where TEndpoint : IEndpoint
        => POSTAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request, sendAsFormData);

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static async Task<HttpResponseMessage> POSTAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                 TRequest request,
                                                                                 bool? sendAsFormData = null) where TEndpoint : IEndpoint
    {
        var (rsp, _) = await POSTAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request, sendAsFormData);

        return rsp;
    }

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> POSTAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => POSTAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new());

    /// <summary>
    /// make a PATCH request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as
    /// well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to PATCH to</param>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static Task<TestResult<TResponse>> PATCHAsync<TRequest, TResponse>(this HttpClient client,
                                                                              string requestUri,
                                                                              TRequest request,
                                                                              bool? sendAsFormData = null)
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Patch, requestUri, request, sendAsFormData);

    /// <summary>
    /// make a PATCH request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static Task<TestResult<TResponse>> PATCHAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                         TRequest request,
                                                                                         bool? sendAsFormData = null) where TEndpoint : IEndpoint
        => PATCHAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request, sendAsFormData);

    /// <summary>
    /// make a PATCH request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static async Task<HttpResponseMessage> PATCHAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                  TRequest request,
                                                                                  bool? sendAsFormData = null) where TEndpoint : IEndpoint
    {
        var (rsp, _) = await PATCHAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request, sendAsFormData);

        return rsp;
    }

    /// <summary>
    /// make a PATCH request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> PATCHAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => PATCHAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new());

    /// <summary>
    /// make a PUT request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as well
    /// as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static Task<TestResult<TResponse>> PUTAsync<TRequest, TResponse>(this HttpClient client,
                                                                            string requestUri,
                                                                            TRequest request,
                                                                            bool? sendAsFormData = null)
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Put, requestUri, request, sendAsFormData);

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static Task<TestResult<TResponse>> PUTAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                       TRequest request,
                                                                                       bool? sendAsFormData = null) where TEndpoint : IEndpoint
        => PUTAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request, sendAsFormData);

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    /// <param name="sendAsFormData">when set to true, the request dto will be automatically converted to a <see cref="MultipartFormDataContent" /></param>
    public static async Task<HttpResponseMessage> PUTAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                TRequest request,
                                                                                bool? sendAsFormData = null) where TEndpoint : IEndpoint
    {
        var (rsp, _) = await PUTAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request, sendAsFormData);

        return rsp;
    }

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> PUTAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => PUTAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new());

    /// <summary>
    /// make a GET request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as well
    /// as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    public static Task<TestResult<TResponse>> GETAsync<TRequest, TResponse>(this HttpClient client,
                                                                            string requestUri,
                                                                            TRequest request)
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Get, requestUri, request);

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    public static Task<TestResult<TResponse>> GETAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                       TRequest request) where TEndpoint : IEndpoint
        => GETAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    public static async Task<HttpResponseMessage> GETAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                TRequest request) where TEndpoint : IEndpoint
    {
        var (rsp, _) = await GETAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

        return rsp;
    }

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing the
    /// <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> GETAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => GETAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new());

    /// <summary>
    /// make a DELETE request using a request dto and get back a <see cref="TestResult{TResponse}" /> containing the <see cref="HttpResponseMessage" /> as
    /// well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TRequest">type of the request dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    public static Task<TestResult<TResponse>> DELETEAsync<TRequest, TResponse>(this HttpClient client,
                                                                               string requestUri,
                                                                               TRequest request)
        => client.SENDAsync<TRequest, TResponse>(HttpMethod.Delete, requestUri, request);

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery using a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    public static Task<TestResult<TResponse>> DELETEAsync<TEndpoint, TRequest, TResponse>(this HttpClient client,
                                                                                          TRequest request) where TEndpoint : IEndpoint
        => DELETEAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    public static async Task<HttpResponseMessage> DELETEAsync<TEndpoint, TRequest>(this HttpClient client,
                                                                                   TRequest request) where TEndpoint : IEndpoint
    {
        var (rsp, _) = await DELETEAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

        return rsp;
    }

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery without a request dto and get back a <see cref="TestResult{TResponse}" /> containing
    /// the <see cref="HttpResponseMessage" /> as well as the <typeparamref name="TResponse" /> DTO.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<TestResult<TResponse>> DELETEAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => DELETEAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new());

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
    public static async Task<TestResult<TResponse>> SENDAsync<TRequest, TResponse>(this HttpClient client,
                                                                                   HttpMethod method,
                                                                                   string requestUri,
                                                                                   TRequest request,
                                                                                   bool? sendAsFormData = null)
    {
        var rsp = await client.SendAsync(
                      new()
                      {
                          Method = method,
                          RequestUri = new($"{client.BaseAddress}{requestUri.TrimStart('/')}"),
                          Content = sendAsFormData is true
                                        ? request.ToForm()
                                        : new StringContent(JsonSerializer.Serialize(request, SerOpts.Options), Encoding.UTF8, "application/json")
                      });

        TResponse? res = default!;

        if (typeof(TResponse) == Types.EmptyResponse)
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

    static MultipartFormDataContent ToForm<TRequest>(this TRequest req)
    {
        var form = new MultipartFormDataContent();

        foreach (var p in req!.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
        {
            if (p is { CanWrite: false, CanRead: false })
                continue;

            if (p.PropertyType == Types.IFormFile)
                AddFileToForm((IFormFile)p.GetValue(req)!, p);

            else if (p.PropertyType.IsAssignableTo(Types.IEnumerableOfIFormFile))
            {
                var files = p.GetValue(req) as IFormFileCollection;

                if (files?.Count is 0 or null)
                    continue;

                foreach (var file in files)
                    AddFileToForm(file, p);
            }
            else
                form.Add(new StringContent(p.GetValue(req)?.ToString() ?? ""), p.Name);
        }

        return !form.Any()
                   ? throw new InvalidOperationException("Converting the request DTO to MultipartFormDataContent was unsuccessful!")
                   : form;

        void AddFileToForm(IFormFile file, MemberInfo prop)
        {
            var content = new StreamContent(file.OpenReadStream());
            if (file.Headers?.ContainsKey(HeaderNames.ContentType) is true)
                content.Headers.ContentType = new(file.ContentType);
            form.Add(content, prop.Name, file.FileName);
        }
    }
}