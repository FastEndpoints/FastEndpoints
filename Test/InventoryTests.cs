using ApiExpress;
using ApiExpress.TestClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class InventoryTests
    {
        [TestMethod]
        public async Task CreateProductClaimMissing()
        {
            var (_, result) = await AdminClient.PostAsync<Inventory.Manage.MissingClaimTest.ThrowIfMissingRequest, ErrorResponse>(
                "/inventory/manage/missing-claim-test",
                new()
                {
                    TestProp = "xyz"
                });

            Assert.AreEqual(400, result?.StatusCode);
            Assert.AreEqual("User doesn't have this claim type!", result?.Errors["null-claim"].First());
        }

        [TestMethod]
        public async Task CreateProductClaimMissingDontThrow()
        {
            var (res, result) = await AdminClient.PostAsync<Inventory.Manage.MissingClaimTest.DontThrowIfMissingRequest, string>(
                "/inventory/manage/missing-claim-test/dont-throw",
                new()
                {
                    TestProp = "xyz"
                });

            Assert.AreEqual(HttpStatusCode.OK, res?.StatusCode);
            Assert.AreEqual("you sent xyz", result);
        }

        [TestMethod]
        public async Task CreateProductFailValidation()
        {
            var (res, result) = await AdminClient.PostAsync<Inventory.Manage.Create.Request, ErrorResponse>(
                "/inventory/manage/create",
                new()
                {
                    Price = 1100
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
            Assert.AreEqual(2, result?.Errors.Count);
            Assert.IsTrue(result?.Errors.ContainsKey("Name"));
            Assert.IsTrue(result?.Errors.ContainsKey("ModifiedBy"));
        }

        [TestMethod]
        public async Task CreateProductFailBusinessLogic()
        {
            var (res, result) = await AdminClient.PostAsync<Inventory.Manage.Create.Request, ErrorResponse>(
                "/inventory/manage/create",
                new()
                {
                    Name = "test item",
                    ModifiedBy = "me",
                    Price = 1100
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
            //Assert.IsTrue(result?.Errors.ContainsKey("Description"));
        }
    }
}
