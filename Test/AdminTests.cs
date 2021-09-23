using ApiExpress;
using ApiExpress.TestClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
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
            var (res, body) = await client.PostAsync<Admin.Login.Request, ErrorResponse>(
                "/admin/login",
                new()
                {
                    UserName = "x",
                    Password = "y"
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
            Assert.AreEqual(2, body?.Errors.Count);
        }

        [TestMethod]
        public async Task AdminLoginInvalidCreds()
        {
            var (res, body) = await client.PostAsync<Admin.Login.Request, Admin.Login.Response>(
                "/admin/login",
                new()
                {
                    UserName = "admin",
                    Password = "xxxxx"
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
        }

        [TestMethod]
        public async Task AdminLoginSuccess()
        {
            var (res, body) = await client.PostAsync<Admin.Login.Request, Admin.Login.Response>(
                "/admin/login",
                new()
                {
                    UserName = "admin",
                    Password = "pass"
                });

            Assert.AreEqual(HttpStatusCode.OK, res?.StatusCode);
            Assert.IsTrue(body?.Permissions?.Count() == 4);
            Assert.IsTrue(body?.JWTToken is not null);
        }
    }
}