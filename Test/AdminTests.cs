using ApiExpress;
using ApiExpress.TestClient;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Threading.Tasks;

namespace Test
{
    [TestClass]
    public class AdminTests
    {
        [TestMethod]
        public async Task AdminLoginWithBadInput()
        {
            var factory = new WebApplicationFactory<Program>(); //todo: move setup to class init

            var client = factory.CreateClient();

            var res = await client.PostAsync<Admin.Login.Request, ErrorResponse>(
                "/admin/login",
                new()
                {
                    UserName = "x",
                    Password = "y"
                });

            Assert.IsNotNull(res);
            Assert.AreEqual(HttpStatusCode.BadRequest, res.Response?.StatusCode);
            Assert.AreEqual(2, res.Body?.Errors.Count);
        }
    }
}