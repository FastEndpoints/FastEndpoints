using ApiExpress;
using ApiExpress.TestClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class InventoryTests
    {
        [TestMethod]
        public async Task CreateNewProductClaimMissing()
        {
            var (_, result) = await AdminClient.PostAsync<Inventory.Manage.MissingClaimTest.Request, ErrorResponse>(
                "/inventory/manage/missing-claim-test",
                new()
                {
                    TestProp = "xyz"
                });

            Assert.AreEqual(400, result?.StatusCode);
            Assert.AreEqual("User doesn't have this claim type!", result?.Errors["testprop"].First());
        }
    }
}
