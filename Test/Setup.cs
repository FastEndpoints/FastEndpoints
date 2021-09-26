using FastEndpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Headers;
using Web.Services;

namespace Test
{
    public static class Setup
    {
        private static readonly WebApplicationFactory<Program> factory = new();

        public static HttpClient AdminClient { get; } = factory
            .WithWebHostBuilder(b =>
                b.ConfigureTestServices(s =>
                    s.AddSingleton<IEmailService, MockEmailService>()))
            .CreateClient();

        public static HttpClient GuestClient { get; } = factory.CreateClient();

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
