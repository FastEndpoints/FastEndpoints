using FastEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class InventoryTests
    {
        [TestMethod]
        public async Task CreateProductFailValidation()
        {
            var (res, result) = await AdminClient.POSTAsync<
                Inventory.Manage.Create.Endpoint,
                Inventory.Manage.Create.Request,
                ErrorResponse>(new()
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
            var (res, result) = await AdminClient.POSTAsync<
                Inventory.Manage.Create.Endpoint,
                Inventory.Manage.Create.Request,
                ErrorResponse>(new()
                {
                    Name = "test item",
                    ModifiedBy = "me",
                    Price = 1100
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
            Assert.AreEqual(2, result?.Errors.Count);
            Assert.IsTrue(result?.Errors.ContainsKey("Description"));
            Assert.IsTrue(result?.Errors.ContainsKey("Price"));
        }

        [TestMethod]
        public async Task CreateProductFailDuplicateItem()
        {
            var (res, result) = await AdminClient.POSTAsync<
                Inventory.Manage.Create.Endpoint,
                Inventory.Manage.Create.Request,
                ErrorResponse>(new()
                {
                    Name = "Apple Juice",
                    Description = "description",
                    ModifiedBy = "me",
                    Price = 100
                });

            Assert.AreEqual(HttpStatusCode.BadRequest, res?.StatusCode);
            Assert.AreEqual(1, result?.Errors.Count);
            Assert.IsTrue(result?.Errors.ContainsKey("GeneralErrors"));
        }

        [TestMethod]
        public async Task CreateProductFailNoPermission()
        {
            try
            {
                var (res, _) = await CustomerClient.PUTAsync<
                    Inventory.Manage.Update.Endpoint,
                    Inventory.Manage.Update.Request,
                    Inventory.Manage.Update.Response>(new()
                    {
                        Name = "Grape Juice",
                        Description = "description",
                        ModifiedBy = "me",
                        Price = 100
                    });
            }
            catch (InvalidOperationException x)
            {
                Assert.IsTrue(x.Message.Contains("Forbidden"));
            }
        }

        [TestMethod]
        public async Task CreateProductSuccess()
        {
            var (res, result) = await AdminClient.POSTAsync<
                Inventory.Manage.Create.Endpoint,
                Inventory.Manage.Create.Request,
                Inventory.Manage.Create.Response>(new()
                {
                    Name = "Grape Juice",
                    Description = "description",
                    ModifiedBy = "me",
                    Price = 100
                });

            Assert.AreEqual(HttpStatusCode.OK, res?.StatusCode);
            Assert.IsTrue(result?.ProductId > 1);
            Assert.AreEqual("Grape Juice", result?.ProductName);
        }

        [TestMethod]
        public async Task ResponseCaching()
        {
            var (_, res1) = await GuestClient.GETAsync<Inventory.GetProduct.Endpoint, Inventory.GetProduct.Response>();

            await Task.Delay(100);

            var (_, res2) = await GuestClient.GETAsync<Inventory.GetProduct.Endpoint, Inventory.GetProduct.Response>();

            Assert.AreEqual(res1?.LastModified, res2?.LastModified);
        }
    }
}
