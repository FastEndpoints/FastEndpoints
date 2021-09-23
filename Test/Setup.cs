using ApiExpress.TestClient;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;

namespace Test
{
    public static class Setup
    {
        public static HttpClient Client { get; set; }
        public static string? AdminAuthToken { get; set; }

        static Setup()
        {
            Client = new WebApplicationFactory<Program>().CreateClient();

            var (_, Body) = Client.PostAsync<Admin.Login.Request, Admin.Login.Response>(
                "/admin/login",
                new()
                {
                    UserName = "admin",
                    Password = "pass"
                }).GetAwaiter().GetResult();

            AdminAuthToken = Body?.JWTToken;
        }
    }
}
