using FastEndpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Headers;

namespace Test
{
    public static class Setup
    {
        private static readonly WebApplicationFactory<Program> factory = new();

        public static HttpClient AdminClient { get; } = factory.CreateClient();
        public static HttpClient GuestClient { get; } = factory.CreateClient();

        static Setup()
        {
            var (_, result) = GuestClient.PostAsync<Admin.Login.Request, Admin.Login.Response>(
                "/admin/login",
                new()
                {
                    UserName = "admin",
                    Password = "pass"
                }).GetAwaiter().GetResult();

            AdminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result?.JWTToken);
        }
    }
}
