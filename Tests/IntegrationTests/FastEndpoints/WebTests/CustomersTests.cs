using System.Globalization;
using System.Net;
using Create = Customers.Create;
using List = Customers.List;
using Orders = Sales.Orders;
using Update = Customers.Update;
using UpdateWithHdr = Customers.UpdateWithHeader;

namespace Web;

public class CustomersTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task ListRecentCustomers()
    {
        var (_, res) = await App.AdminClient.GETAsync<List.Recent.Endpoint, List.Recent.Response>();

        res.Customers!.Count().ShouldBe(3);
        res.Customers!.First().Key.ShouldBe("ryan gunner");
        res.Customers!.Last().Key.ShouldBe("ryan reynolds");
    }

    [Fact]
    public async Task ListRecentCustomersCookieScheme()
    {
        var (rsp, _) = await App.AdminClient.GETAsync<List.Recent.Endpoint_V1, List.Recent.Response>();

        rsp.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task CreateNewCustomer()
    {
        var (rsp, res) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, string>(
                             new()
                             {
                                 CreatedBy = "this should be replaced by claim",
                                 CustomerName = "test customer",
                                 PhoneNumbers = ["123", "456"]
                             });

        rsp.StatusCode.ShouldBe(HttpStatusCode.OK);
        res.ShouldBe("Email was not sent during testing! admin");
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

        res.ShouldBe("CST001");
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

        res.ShouldBe("customer id set by admin user");
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

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.OrderID.ShouldBe(54321);
        res.AnotherMsg.ShouldBe("Email was not sent during testing!");
        res.Event.One.ShouldBe(100);
        res.Event.Two.ShouldBe(200);

        res.Header1.ShouldBe(0);
        res.Header2.ShouldBe(default);
        rsp.Headers.GetValues("x-header-one").Single().ShouldBe("12345");
        DateOnly.Parse(rsp.Headers.GetValues("Header2").Single(), CultureInfo.InvariantCulture).ShouldBe(new(2020, 11, 12));
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

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.OrderID.ShouldBe(54321);
        res.GuidTest.ShouldBe(guid);
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
        results[0].ShouldBe("qwerty");
        results[1].ShouldBe("123");
    }
}