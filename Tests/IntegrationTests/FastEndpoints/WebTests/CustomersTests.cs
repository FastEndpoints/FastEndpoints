using IntegrationTests.Shared.Fixtures;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.WebTests;

public class CustomersTests : EndToEndTestBase
{
    public CustomersTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
    }

    [Fact]
    public async Task ListRecentCustomers()
    {
        var (_, res) = await AdminClient.GETAsync<
            Customers.List.Recent.Endpoint,
            Customers.List.Recent.Response>();

        res?.Customers?.Count().Should().Be(3);
        res?.Customers?.First().Key.Should().Be("ryan gunner");
        res?.Customers?.Last().Key.Should().Be("ryan reynolds");
    }

    [Fact]
    public async Task ListRecentCustomersCookieScheme()
    {
        var (rsp, _) = await AdminClient.GETAsync<
            Customers.List.Recent.Endpoint_V1,
            Customers.List.Recent.Response>();

        rsp!.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
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

        rsp?.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Should().Be("Email was not sent during testing! admin");

    }

    [Fact]
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

        res.Should().Be("CST001");
    }

    [Fact]
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

        res.Should().Be("customer id set by admin user");
    }

    [Fact]
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

        rsp?.IsSuccessStatusCode.Should().BeTrue();
        res?.OrderID.Should().Be(54321);
        res?.AnotherMsg.Should().Be("Email actually sent!");
        res?.Event.One.Should().Be(100);
        res?.Event.Two.Should().Be(200);
    }

    [Fact]
    public async Task CreateOrderByCustomerGuidTest()
    {
        var guid = Guid.NewGuid();

        var (rsp, res) = await CustomerClient.POSTAsync<
            Sales.Orders.Create.Request,
            Sales.Orders.Create.Response>(
            $"api/sales/orders/create/{guid}",
            new()
            {
                CustomerID = 12345,
                ProductID = 100,
                Quantity = 23,
                GuidTest = Guid.NewGuid()
            });

        rsp?.IsSuccessStatusCode.Should().BeTrue();
        res?.OrderID.Should().Be(54321);
        res!.GuidTest.Should().Be(guid);
    }

    [Fact]
    public async Task CustomerUpdateByCustomerWithTenantIDInHeader()
    {
        var (_, res) = await CustomerClient.PUTAsync<
            Customers.UpdateWithHeader.Endpoint,
            Customers.UpdateWithHeader.Request,
            string>(new(
                10,
                "this will be set to qwerty from header",
                "test customer",
                123,
                "address"));

        var results = res!.Split('|');
        results[0].Should().Be("qwerty");
        results[1].Should().Be("123");
    }
}