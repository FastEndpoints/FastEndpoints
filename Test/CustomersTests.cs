using FastEndpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using Web.Services;
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
            var (rsp, res) = await AdminClient.PostAsync<Customers.Create.Endpoint, Customers.Create.Request, string>(new()
            {
                CreatedBy = "this should be replace by claim",
                CustomerName = "test customer"
            });

            Assert.AreEqual(HttpStatusCode.OK, rsp?.StatusCode);
            Assert.AreEqual("Email was not sent during testing!" + " test customer", res);
        }
    }
}
