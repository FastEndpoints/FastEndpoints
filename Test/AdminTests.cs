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
            var (resp, result) = await GuestClient.POSTAsync<
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
            var (res, _) = await GuestClient.POSTAsync<
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
            var (resp, result) = await GuestClient.POSTAsync<
                Admin.Login.Endpoint,
                Admin.Login.Request,
                Admin.Login.Response>(new()
                {
                    UserName = "admin",
                    Password = "pass"
                });

            Assert.AreEqual(HttpStatusCode.OK, resp?.StatusCode);
            Assert.AreEqual(7, result?.Permissions?.Count());
            Assert.IsTrue(result?.JWTToken is not null);
        }

        [TestMethod]
        public async Task AdminLoginV2()
        {
            var (resp, result) = await GuestClient.GETAsync<
                Admin.Login.Endpoint_V2,
                EmptyRequest,
                int>(new());

            Assert.AreEqual(HttpStatusCode.OK, resp?.StatusCode);
            Assert.AreEqual(2, result);
        }
    }
}