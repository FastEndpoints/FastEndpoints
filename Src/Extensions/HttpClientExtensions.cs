using System.Net.Http.Json;
using System.Text.Json;

namespace FastEndpoints
{
    public static class HttpClientExtensions
    {
        public static async Task<(HttpResponseMessage? response, TResponse? result)> PostAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
            where TResponse : class
        {
            var res = await client.PostAsJsonAsync(requestUri, request, EndpointBase.SerializerOptions).ConfigureAwait(false);

            TResponse? body;

            try
            {
                body = await res.Content.ReadFromJsonAsync<TResponse>(EndpointBase.SerializerOptions).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException($"Unable to deserialize the response body as [{typeof(TResponse).FullName}]");
            }

            return (res, body);
        }

        public static Task<(HttpResponseMessage? response, TResponse? result)> PostAsync<TEndpoint, TRequest, TResponse>
            (this HttpClient client, TRequest request)
            where TEndpoint : EndpointBase, new()
            where TResponse : class
                => PostAsync<TRequest, TResponse>(client, new TEndpoint().GetTestURL(), request);

        public static async Task<(HttpResponseMessage? response, TResponse? result)> PutAsync<TRequest, TResponse>
        (this HttpClient client, string requestUri, TRequest request)
        where TResponse : class
        {
            var res = await client.PutAsJsonAsync(requestUri, request, EndpointBase.SerializerOptions).ConfigureAwait(false);

            TResponse? body;

            try
            {
                body = await res.Content.ReadFromJsonAsync<TResponse>(EndpointBase.SerializerOptions).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException($"Unable to deserialize the response body as [{typeof(TResponse).FullName}]");
            }

            return (res, body);
        }

        public static Task<(HttpResponseMessage? response, TResponse? result)> PutAsync<TEndpoint, TRequest, TResponse>
            (this HttpClient client, TRequest request)
            where TEndpoint : EndpointBase, new()
            where TResponse : class
                => PutAsync<TRequest, TResponse>(client, new TEndpoint().GetTestURL(), request);

        public static Task<TResponse?> GetAsync<TResponse>(this HttpClient client, string requestUri)
        {
            return client.GetFromJsonAsync<TResponse>(requestUri, EndpointBase.SerializerOptions);
        }

        public static Task<TResponse?> GetAsync<TEndpoint, TResponse>(this HttpClient client)
            where TEndpoint : EndpointBase, new()
                => GetAsync<TResponse>(client, new TEndpoint().GetTestURL());
    }
}