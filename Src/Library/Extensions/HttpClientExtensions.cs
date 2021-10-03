using System.Net.Http.Json;
using System.Text.Json;

namespace FastEndpoints
{
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
        public static async Task<(HttpResponseMessage? response, TResponse? result)> PostAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
        {
            var res = await client.PostAsJsonAsync(requestUri, request, BaseEndpoint.SerializerOptions).ConfigureAwait(false);

            TResponse? body;

            try
            {
                body = await res.Content.ReadFromJsonAsync<TResponse>(BaseEndpoint.SerializerOptions).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(
                    $"Unable to deserialize the response body as [{typeof(TResponse).FullName}]. Reason: {res.StatusCode}");
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
        public static Task<(HttpResponseMessage? response, TResponse? result)> PostAsync<TEndpoint, TRequest, TResponse>
            (this HttpClient client, TRequest request) where TEndpoint : BaseEndpoint, new()
                => PostAsync<TRequest, TResponse>(client, new TEndpoint().GetTestURL(), request);

        /// <summary>
        /// make a PUT request using a request dto and get back a response dto.
        /// </summary>
        /// <typeparam name="TRequest">type of the requet dto</typeparam>
        /// <typeparam name="TResponse">type of the response dto</typeparam>
        /// <param name="requestUri">the route url to post to</param>
        /// <param name="request">the request dto</param>
        /// <exception cref="InvalidOperationException">thrown when the response body cannot be deserialized in to specified response dto type</exception>
        public static async Task<(HttpResponseMessage? response, TResponse? result)> PutAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
        {
            var res = await client.PutAsJsonAsync(requestUri, request, BaseEndpoint.SerializerOptions).ConfigureAwait(false);

            TResponse? body;

            try
            {
                body = await res.Content.ReadFromJsonAsync<TResponse>(BaseEndpoint.SerializerOptions).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException(
                    $"Unable to deserialize the response body as [{typeof(TResponse).FullName}]. Reason: {res.StatusCode}");
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
        public static Task<(HttpResponseMessage? response, TResponse? result)> PutAsync<TEndpoint, TRequest, TResponse>
            (this HttpClient client, TRequest request) where TEndpoint : BaseEndpoint, new()
                => PutAsync<TRequest, TResponse>(client, new TEndpoint().GetTestURL(), request);

        /// <summary>
        /// make a GET request to an endpoint using a route url and get back a response dto.
        /// </summary>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        /// <param name="requestUri">the route url to connect to</param>
        public static Task<TResponse?> GetAsync<TResponse>(this HttpClient client, string requestUri)
        {
            return client.GetFromJsonAsync<TResponse>(requestUri, BaseEndpoint.SerializerOptions);
        }

        /// <summary>
        /// make a GET request to an endpoint using auto route discovery and get back a response dto.
        /// </summary>
        /// <typeparam name="TEndpoint">the type of the request dto</typeparam>
        /// <typeparam name="TResponse">the type of the response dto</typeparam>
        public static Task<TResponse?> GetAsync<TEndpoint, TResponse>(this HttpClient client)
            where TEndpoint : BaseEndpoint, new()
                => GetAsync<TResponse>(client, new TEndpoint().GetTestURL());
    }
}