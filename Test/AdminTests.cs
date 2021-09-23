using ApiExpress;
using ApiExpress.TestClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class AdminTests
    {
        [TestMethod]
        public async Task AdminLoginWithBadInput()
        {
            var (resp, result) = await GuestClient.PostAsync<Admin.Login.Request, ErrorResponse>(
                "/admin/login",
                new()
                {
                    UserName = "x",
                    Password = "y"
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, resp?.StatusCode);
            Assert.AreEqual(2, result?.Errors.Count);
        }

        [TestMethod]
        public async Task AdminLoginInvalidCreds()
        {
            var (res, _) = await GuestClient.PostAsync<Admin.Login.Request, Admin.Login.Response>(
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
            var (resp, result) = await GuestClient.PostAsync<Admin.Login.Request, Admin.Login.Response>(
                "/admin/login",
                new()
                {
                    UserName = "admin",
                    Password = "pass"
                });

            Assert.AreEqual(HttpStatusCode.OK, resp?.StatusCode);
            Assert.IsTrue(result?.Permissions?.Count() == 4);
            Assert.IsTrue(result?.JWTToken is not null);
        }
    }
}