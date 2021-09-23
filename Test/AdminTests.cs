using ApiExpress;
using ApiExpress.TestClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Test
{
    [TestClass]
    public class AdminTests
    {
        private static readonly HttpClient client = Setup.Client;

        [TestMethod]
        public async Task AdminLoginWithBadInput()
        {
            var res = await client.PostAsync<Admin.Login.Request, ErrorResponse>(
                "/admin/login",
                new()
                {
                    UserName = "x",
                    Password = "y"
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res.Response?.StatusCode);
            Assert.AreEqual(2, res.Body?.Errors.Count);
        }
    }
}