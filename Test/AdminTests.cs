using FastEndpoints;
using Microsoft.AspNetCore.Mvc.Testing;
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

        //[TestMethod]
        public async Task AdminLoginThrottling()
        {
            var guestClient = new WebApplicationFactory<Program>().CreateClient();
            guestClient.DefaultRequestHeaders.Add("X-Forwarded-For", "TEST");

            int successCount = 0;

            for (int i = 1; i <= 6; i++)
            {
                try
                {
                    var (rsp, _) = await guestClient.POSTAsync<
                        Admin.Login.Endpoint_V1,
                        Admin.Login.Request,
                        Admin.Login.Response>(new()
                        {
                            UserName = "admin",
                            Password = "pass"
                        });
                    Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
                    successCount++;
                }
                catch (Exception x)
                {
                    Assert.AreEqual(6, i);
                    Assert.IsTrue(x.GetType() == typeof(InvalidOperationException));
                }
            }

            Assert.AreEqual(5, successCount);
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