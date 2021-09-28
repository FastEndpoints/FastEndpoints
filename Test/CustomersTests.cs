using FastEndpoints;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using static Test.Setup;

namespace Test
{
    [TestClass]
    public class CustomersTests
    {
        [TestMethod]
        public async Task ListRecentCustomers()
        {
            var res = await AdminClient.GetAsync<
                Customers.List.Recent.Endpoint,
                Customers.List.Recent.Response>();

            Assert.AreEqual(3, res?.Customers?.Count());
            Assert.AreEqual("ryan gunner", res?.Customers?.First().Key);
            Assert.AreEqual("ryan reynolds", res?.Customers?.Last().Key);
        }

        [TestMethod]
        public async Task CreateNewCustomer()
        {
            var (rsp, res) = await AdminClient.PostAsync<
                Customers.Create.Endpoint,
                Customers.Create.Request,
                string>(new()
                {
                    CreatedBy = "this should be replaced by claim",
                    CustomerName = "test customer"
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("Email was not sent during testing! admin", res);
        }

        [TestMethod]
        public async Task CustomerUpdateByCustomer()
        {
            var (_, res) = await CustomerClient.PutAsync<
                Customers.Update.Endpoint,
                Customers.Update.Request,
                string>(new()
                {
                    CustomerID = "this will be auto bound from claim",
                    Address = "address",
                    Age = 123,
                    Name = "test customer"
                });

            Assert.AreEqual("CST001", res);
        }

        [TestMethod]
        public async Task CustomerUpdateAdmin()
        {
            var (_, res) = await AdminClient.PutAsync<
                Customers.Update.Endpoint,
                Customers.Update.Request,
                string>(new()
                {
                    CustomerID = "customer id set by admin user",
                    Address = "address",
                    Age = 123,
                    Name = "test customer"
                });

            Assert.AreEqual("customer id set by admin user", res);
        }
    }
}
