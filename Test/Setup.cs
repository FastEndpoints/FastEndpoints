using FastEndpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;

namespace Test
{
    public static class Setup
    {
        private static readonly WebApplicationFactory<Program> appFactory = new();

        public static HttpClient AdminClient { get; } = appFactory.CreateClient();
        public static HttpClient GuestClient { get; } = appFactory.CreateClient();

        static Setup()
        {
            var (_, result) = GuestClient.PostAsync<
                Admin.Login.Endpoint,
                Admin.Login.Request,
                Admin.Login.Response>(new()
                {
                    UserName = "admin",
                    Password = "pass"
                })
                .GetAwaiter()
                .GetResult();

            AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result?.JWTToken);
        }
    }
}
