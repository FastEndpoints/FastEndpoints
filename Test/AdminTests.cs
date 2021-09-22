using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Test
{
    [TestClass]
    public class AdminTests
    {
        [TestMethod]
        public async Task AdminLoginWithBadInput()
        {
            var app = new WebApplicationFactory<Program>();

            var client = app.CreateClient();

            var res = await client.PostAsJsonAsync<Admin.Login.Request>(
                "/admin/login",
                new()
                {
                    UserName = "x",
                    Password = "y"
                });

            Assert.IsNotNull(res);
            Assert.AreEqual(HttpStatusCode.OK, res.StatusCode);

            var r = res.Content.ReadFromJsonAsync<Admin.Login.Response>();
        }
    }
}