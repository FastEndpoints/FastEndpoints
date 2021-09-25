using FastEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class CustomersTests
    {
        [TestMethod]
        public async Task ListRecentCustomers()
        {
            var res = await AdminClient.GetAsync<Customers.List.Recent.Response>("/customers/list/recent");

            Assert.AreEqual(3, res?.Customers?.Count());
            Assert.AreEqual("ryan gunner", res?.Customers?.First().Key);
            Assert.AreEqual("ryan reynolds", res?.Customers?.Last().Key);

        }
    }
}
