using FastEndpoints;
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
            var (resp, result) = await GuestClient.PostAsync<
                Admin.Login.Endpoint,
                Admin.Login.Request,
                ErrorResponse>(new()
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
            var (res, _) = await GuestClient.PostAsync<
                Admin.Login.Endpoint,
                Admin.Login.Request,
                Admin.Login.Response>(new()
                {
                    UserName = "admin",
                    Password = "xxxxx"
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
        }

        [TestMethod]
        public async Task AdminLoginSuccess()
        {
            var (resp, result) = await GuestClient.PostAsync<
                Admin.Login.Endpoint,
                Admin.Login.Request,
                Admin.Login.Response>(new()
                {
                    UserName = "admin",
                    Password = "pass"
                });

            Assert.AreEqual(HttpStatusCode.OK, resp?.StatusCode);
            Assert.AreEqual(8, result?.Permissions?.Count());
            Assert.IsTrue(result?.JWTToken is not null);
        }
    }
}