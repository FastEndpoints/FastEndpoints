using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// a set of extensions to the httpclient in order to facilitate route-less integration testing
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// make a POST request using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TRequest">type of the requet dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <exception cref="InvalidOperationException">thrown when the response body cannot be deserialized in to specified response dto type</exception>
    public static async Task<(HttpResponseMessage? response, TResponse? result)>
        POSTAsync<TRequest, TResponse>(this HttpClient client, string requestUri, TRequest request)
    {
        var res = await client.PostAsJsonAsync(requestUri, request, SerOpts.Options);

        if (typeof(TResponse) == Types.EmptyResponse)
            return (res, default(TResponse));

        TResponse? body;

        try
        {
            body = await res.Content.ReadFromJsonAsync<TResponse>(SerOpts.Options);
        }
        catch (JsonException)
        {
            var reason = $"[{res.StatusCode}] {await res.Content.ReadAsStringAsync()}";
            throw new InvalidOperationException(
                $"Unable to deserialize the response body as [{typeof(TResponse).FullName}]. Reason: {reason}");
        }

        return (res, body);
    }

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    public static Task<(HttpResponseMessage? response, TResponse? result)>
        POSTAsync<TEndpoint, TRequest, TResponse>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
        => POSTAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    public static async Task<HttpResponseMessage?> POSTAsync<TEndpoint, TRequest>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
    {
        var (response, _) = await POSTAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);
        return response;
    }

    /// <summary>
    /// make a POST request to an endpoint using auto route discovery without a request dto and get back a typed response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<(HttpResponseMessage? response, TResponse? result)> POSTAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => POSTAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new EmptyRequest());

    /// <summary>
    /// make a PUT request using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TRequest">type of the requet dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <exception cref="InvalidOperationException">thrown when the response body cannot be deserialized in to specified response dto type</exception>
    public static async Task<(HttpResponseMessage? response, TResponse? result)> PUTAsync<TRequest, TResponse>(this HttpClient client, string requestUri, TRequest request)
    {
        var res = await client.PutAsJsonAsync(requestUri, request, SerOpts.Options);

        if (typeof(TResponse) == Types.EmptyResponse)
            return (res, default(TResponse));

        TResponse? body;

        try
        {
            body = await res.Content.ReadFromJsonAsync<TResponse>(SerOpts.Options);
        }
        catch (JsonException)
        {
            var reason = $"[{res.StatusCode}] {await res.Content.ReadAsStringAsync()}";
            throw new InvalidOperationException(
                $"Unable to deserialize the response body as [{typeof(TResponse).FullName}]. Reason: {reason}");
        }

        return (res, body);
    }

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    public static Task<(HttpResponseMessage? response, TResponse? result)> PUTAsync<TEndpoint, TRequest, TResponse>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
        => PUTAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    public static async Task<HttpResponseMessage?> PUTAsync<TEndpoint, TRequest>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
    {
        var (response, _) = await PUTAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);
        return response;
    }

    /// <summary>
    /// make a PUT request to an endpoint using auto route discovery without a request dto and get back a typed response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<(HttpResponseMessage? response, TResponse? result)> PUTAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => PUTAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new EmptyRequest());

    /// <summary>
    /// make a GET request using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TRequest">type of the requet dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <exception cref="InvalidOperationException">thrown when the response body cannot be deserialized in to specified response dto type</exception>
    public static async Task<(HttpResponseMessage? response, TResponse? result)> GETAsync<TRequest, TResponse>(this HttpClient client, string requestUri, TRequest request)
    {
        var res = await client.SendAsync(
            new HttpRequestMessage
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri(
                    client.BaseAddress!.ToString().TrimEnd('/') +
                    (requestUri.StartsWith('/') ? requestUri : "/" + requestUri)),
                Content = new StringContent(JsonSerializer.Serialize(request, SerOpts.Options), Encoding.UTF8, "application/json")
            });

        if (typeof(TResponse) == Types.EmptyResponse)
            return (res, default(TResponse));

        TResponse? body;

        try
        {
            body = await res.Content.ReadFromJsonAsync<TResponse>(SerOpts.Options);
        }
        catch (JsonException)
        {
            var reason = $"[{res.StatusCode}] {await res.Content.ReadAsStringAsync()}";
            throw new InvalidOperationException(
                $"Unable to deserialize the response body as [{typeof(TResponse).FullName}]. Reason: {reason}");
        }

        return (res, body);
    }

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    public static Task<(HttpResponseMessage? response, TResponse? result)> GETAsync<TEndpoint, TRequest, TResponse>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
        => GETAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    public static async Task<HttpResponseMessage?> GETAsync<TEndpoint, TRequest>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
    {
        var (response, _) = await GETAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);
        return response;
    }

    /// <summary>
    /// make a GET request to an endpoint using auto route discovery without a request dto and get back a typed response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<(HttpResponseMessage? response, TResponse? result)> GETAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => GETAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new EmptyRequest());

    /// <summary>
    /// make a DELETE request using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TRequest">type of the requet dto</typeparam>
    /// <typeparam name="TResponse">type of the response dto</typeparam>
    /// <param name="requestUri">the route url to post to</param>
    /// <param name="request">the request dto</param>
    /// <exception cref="InvalidOperationException">thrown when the response body cannot be deserialized in to specified response dto type</exception>
    public static async Task<(HttpResponseMessage? response, TResponse? result)> DELETEAsync<TRequest, TResponse>(this HttpClient client, string requestUri, TRequest request)
    {
        var res = await client.SendAsync(
            new HttpRequestMessage
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(
                    client.BaseAddress!.ToString().TrimEnd('/') +
                    (requestUri.StartsWith('/') ? requestUri : "/" + requestUri)),
                Content = new StringContent(JsonSerializer.Serialize(request, SerOpts.Options), Encoding.UTF8, "application/json")
            });

        if (typeof(TResponse) == Types.EmptyResponse)
            return (res, default(TResponse));

        TResponse? body;

        try
        {
            body = await res.Content.ReadFromJsonAsync<TResponse>(SerOpts.Options);
        }
        catch (JsonException)
        {
            var reason = $"[{res.StatusCode}] {await res.Content.ReadAsStringAsync()}";
            throw new InvalidOperationException(
                $"Unable to deserialize the response body as [{typeof(TResponse).FullName}]. Reason: {reason}");
        }

        return (res, body);
    }

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery using a request dto and get back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    /// <param name="request">the request dto</param>
    public static Task<(HttpResponseMessage? response, TResponse? result)> DELETEAsync<TEndpoint, TRequest, TResponse>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
        => DELETEAsync<TRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery using a request dto that does not send back a response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TRequest">the type of the request dto</typeparam>
    /// <param name="request">the request dto</param>
    public static async Task<HttpResponseMessage?> DELETEAsync<TEndpoint, TRequest>(this HttpClient client, TRequest request) where TEndpoint : IEndpoint
    {
        var (response, _) = await DELETEAsync<TRequest, EmptyResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), request);
        return response;
    }

    /// <summary>
    /// make a DELETE request to an endpoint using auto route discovery without a request dto and get back a typed response dto.
    /// </summary>
    /// <typeparam name="TEndpoint">the type of the endpoint</typeparam>
    /// <typeparam name="TResponse">the type of the response dto</typeparam>
    public static Task<(HttpResponseMessage? response, TResponse? result)> DELETEAsync<TEndpoint, TResponse>(this HttpClient client) where TEndpoint : IEndpoint
        => DELETEAsync<EmptyRequest, TResponse>(client, IEndpoint.TestURLFor<TEndpoint>(), new EmptyRequest());
}
