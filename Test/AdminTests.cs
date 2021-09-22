using ApiExpress;
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
            var app = new WebApplicationFactory<Program>(); //todo: move setup to class init

            var client = app.CreateClient();

            var res = await client.PostAsJsonAsync<Admin.Login.Request>(
                "/admin/login",
                new()
                {
                    UserName = "x",
                    Password = "y"
                });

            Assert.IsNotNull(res);
            Assert.AreEqual(HttpStatusCode.BadRequest, res.StatusCode);

            var r = await res.Content.ReadFromJsonAsync<ErrorResponse>();

            Assert.AreEqual(2, r.Errors.Count);
        }
    }
}