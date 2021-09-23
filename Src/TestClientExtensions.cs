using System.Net.Http.Json;

namespace ApiExpress.TestClient
{
    public static class TestClientExtensions
    {
        public static async Task<(HttpResponseMessage? Response, TResponse? Body)> PostAsync<TRequest, TResponse>
            (this HttpClient client, string requestUri, TRequest request)
            where TRequest : IRequest
            where TResponse : IResponse
        {
            var res = await client.PostAsJsonAsync(requestUri, request).ConfigureAwait(false);
            var body = await res.Content.ReadFromJsonAsync<TResponse>().ConfigureAwait(false);
            return (res, body);
        }
    }
}