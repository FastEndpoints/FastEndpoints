using System.Globalization;
using System.Net;
using Create = Customers.Create;
using List = Customers.List;
using Orders = Sales.Orders;
using Update = Customers.Update;
using UpdateWithHdr = Customers.UpdateWithHeader;

namespace Web;

public class CustomersTests(AppFixture App) : TestBase<AppFixture>
{
    [Fact]
    public async Task ListRecentCustomers()
    {
        var (_, res) = await App.AdminClient.GETAsync<List.Recent.Endpoint, List.Recent.Response>();

        res.Customers!.Count().Should().Be(3);
        res.Customers!.First().Key.Should().Be("ryan gunner");
        res.Customers!.Last().Key.Should().Be("ryan reynolds");
    }

    [Fact]
    public async Task ListRecentCustomersCookieScheme()
    {
        var (rsp, _) = await App.AdminClient.GETAsync<List.Recent.Endpoint_V1, List.Recent.Response>();

        rsp.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateNewCustomer()
    {
        var (rsp, res) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, string>(
                             new()
                             {
                                 CreatedBy = "this should be replaced by claim",
                                 CustomerName = "test customer",
                                 PhoneNumbers = new[] { "123", "456" }
                             });

        rsp.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Should().Be("Email was not sent during testing! admin");
    }

    [Fact]
    public async Task CustomerUpdateByCustomer()
    {
        var (_, res) = await App.CustomerClient.PUTAsync<Update.Endpoint, Update.Request, string>(
                           new()
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
        var (_, res) = await App.AdminClient.PUTAsync<Update.Endpoint, Update.Request, string>(
                           new()
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
        var (rsp, res) = await App.CustomerClient.POSTAsync<Orders.Create.Endpoint, Orders.Create.Request, Orders.Create.Response>(
                             new()
                             {
                                 CustomerID = 12345,
                                 ProductID = 100,
                                 Quantity = 23
                             });

        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.OrderID.Should().Be(54321);
        res.AnotherMsg.Should().Be("Email was not sent during testing!");
        res.Event.One.Should().Be(100);
        res.Event.Two.Should().Be(200);

        res.Header1.Should().Be(0);
        res.Header2.Should().Be(default);
        rsp.Headers.GetValues("x-header-one").Single().Should().Be("12345");
        DateOnly.Parse(rsp.Headers.GetValues("Header2").Single(), CultureInfo.InvariantCulture).Should().Be(new(2020, 11, 12));
    }

    [Fact]
    public async Task CreateOrderByCustomerGuidTest()
    {
        var guid = Guid.NewGuid();

        var (rsp, res) = await App.CustomerClient.POSTAsync<Orders.Create.Request, Orders.Create.Response>(
                             $"api/sales/orders/create/{guid}",
                             new()
                             {
                                 CustomerID = 12345,
                                 ProductID = 100,
                                 Quantity = 23,
                                 GuidTest = Guid.NewGuid()
                             });

        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.OrderID.Should().Be(54321);
        res.GuidTest.Should().Be(guid);
    }

    [Fact]
    public async Task CustomerUpdateByCustomerWithTenantIDInHeader()
    {
        var (_, res) = await App.CustomerClient.PUTAsync<UpdateWithHdr.Endpoint, UpdateWithHdr.Request, string>(
                           new(
                               CustomerID: 10,
                               TenantID: "this will be set to qwerty from header",
                               Name: "test customer",
                               Age: 123,
                               Address: "address"));

        var results = res!.Split('|');
        results[0].Should().Be("qwerty");
        results[1].Should().Be("123");
    }
}