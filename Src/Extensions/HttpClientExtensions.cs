using System.Net.Http.Json;

namespace FastEndpoints
{
    public static class HttpClientExtensions
    {
        public static async Task<(HttpResponseMessage? response, TResponse? result)> PostAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
            where TRequest : IRequest
            where TResponse : class
        {
            var res = await client.PostAsJsonAsync(requestUri, request, EndpointBase.SerializerOptions).ConfigureAwait(false);
            var body = await res.Content.ReadFromJsonAsync<TResponse>(EndpointBase.SerializerOptions).ConfigureAwait(false);
            return (res, body);
        }

        public static async Task<(HttpResponseMessage? response, TResponse? result)> PutAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
            where TRequest : IRequest
            where TResponse : class
        {
            var res = await client.PutAsJsonAsync(requestUri, request, EndpointBase.SerializerOptions).ConfigureAwait(false);
            var body = await res.Content.ReadFromJsonAsync<TResponse>(EndpointBase.SerializerOptions).ConfigureAwait(false);
            return (res, body);
        }

        public static Task<TResponse?> GetAsync<TResponse>(this HttpClient client, string requestUri) where TResponse : IResponse
        {
            return client.GetFromJsonAsync<TResponse>(requestUri, EndpointBase.SerializerOptions);
        }
    }
}