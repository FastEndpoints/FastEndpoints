using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http;

namespace Test
{
    public static class Setup
    {
        public static HttpClient Client { get; set; }

        static Setup()
        {
            Client = new WebApplicationFactory<Program>().CreateClient();
        }
    }
}
