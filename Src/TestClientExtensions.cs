using System.Net.Http.Json;

namespace ApiExpress.TestClient
{
    public static class TestClientExtensions
    {
        public static async Task<(HttpResponseMessage? response, TResponse? result)> PostAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
            where TRequest : IRequest
            where TResponse : IResponse
        {
            var res = await client.PostAsJsonAsync(requestUri, request, Endpoint.SerializerOptions).ConfigureAwait(false);
            var body = await res.Content.ReadFromJsonAsync<TResponse>(Endpoint.SerializerOptions).ConfigureAwait(false);
            return (res, body);
        }

        public static Task<TResponse?> GetAsync<TResponse>(this HttpClient client, string requestUri) where TResponse : IResponse
        {
            return client.GetFromJsonAsync<TResponse>(requestUri, Endpoint.SerializerOptions);
        }
    }
}