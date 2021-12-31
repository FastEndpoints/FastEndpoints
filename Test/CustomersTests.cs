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
            var (_, res) = await AdminClient.GETAsync<
                Customers.List.Recent.Endpoint,
                Customers.List.Recent.Response>();

            Assert.AreEqual(3, res?.Customers?.Count());
            Assert.AreEqual("ryan gunner", res?.Customers?.First().Key);
            Assert.AreEqual("ryan reynolds", res?.Customers?.Last().Key);
        }

        [TestMethod]
        public async Task CreateNewCustomer()
        {
            var (rsp, res) = await AdminClient.POSTAsync<
                Customers.Create.Endpoint,
                Customers.Create.Request,
                string>(new()
                {
                    CreatedBy = "this should be replaced by claim",
                    CustomerName = "test customer",
                    PhoneNumbers = new[] { "123", "456" }
                });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("Email was not sent during testing! admin", res);
        }

        [TestMethod]
        public async Task CustomerUpdateByCustomer()
        {
            var (_, res) = await CustomerClient.PUTAsync<
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
            var (_, res) = await AdminClient.PUTAsync<
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

        [TestMethod]
        public async Task CreateOrderByCustomer()
        {
            var (rsp, res) = await CustomerClient.POSTAsync<
                Sales.Orders.Create.Endpoint,
                Sales.Orders.Create.Request,
                Sales.Orders.Create.Response>(new()
                {
                    CustomerID = 12345,
                    ProductID = 100,
                    Quantity = 23
                });

            Assert.IsTrue(rsp?.IsSuccessStatusCode);
            Assert.AreEqual(res?.OrderID, 54321);
            Assert.AreEqual("Email actually sent!", res?.AnotherMsg);
        }

        [TestMethod]
        public async Task CreateOrderByCustomerGuidTest()
        {
            var guid = Guid.NewGuid();

            var (rsp, res) = await CustomerClient.POSTAsync<
                Sales.Orders.Create.Request,
                Sales.Orders.Create.Response>(
                $"/sales/orders/create/{guid}",
                new()
                {
                    CustomerID = 12345,
                    ProductID = 100,
                    Quantity = 23,
                    GuidTest = Guid.NewGuid()
                });

            Assert.IsTrue(rsp?.IsSuccessStatusCode);
            Assert.AreEqual(res?.OrderID, 54321);
            Assert.AreEqual(guid, res!.GuidTest);
        }

        [TestMethod]
        public async Task CustomerUpdateByCustomerWithTenantIDInHeader()
        {
            var (_, res) = await CustomerClient.PUTAsync<
                Customers.UpdateWithHeader.Endpoint,
                Customers.UpdateWithHeader.Request,
                string>(new()
                {
                    CustomerID = "this will be auto bound from claim",
                    Address = "address",
                    Age = 123,
                    Name = "test customer",
                    TenantID = "this will be set to qwerty from header"
                });

            Assert.AreEqual("qwerty", res);
        }
    }
}
