using System.Net;
using Create = Inventory.Manage.Create;
using Delete = Inventory.Manage.Delete;
using GetProduct = Inventory.GetProduct;
using Update = Inventory.Manage.Update;

namespace Web;

public class InventoryTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task CreateProductFailValidation()
    {
        var (res, result) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, ErrorResponse>(
                                new()
                                {
                                    Price = 1100
                                });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContainKey("name");
        result.Errors.ShouldContainKey("modifiedBy");
    }

    [Fact]
    public async Task CreateProductFailBusinessLogic()
    {
        var (res, result) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, ErrorResponse>(
                                new()
                                {
                                    Name = "test item",
                                    ModifiedBy = "me",
                                    Price = 1100
                                });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Errors.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(2);
        result.Errors.ShouldContainKey("description");
        result.Errors.ShouldContainKey("price");
    }

    [Fact]
    public async Task CreateProductFailDuplicateItem()
    {
        var (res, result) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, ErrorResponse>(
                                new()
                                {
                                    Name = "Apple Juice",
                                    Description = "description",
                                    ModifiedBy = "me",
                                    Price = 100
                                });

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.Errors.ShouldNotBeNull();
        result.Errors.Count.ShouldBe(1);
        result.Errors.ShouldContainKey("generalErrors");
    }

    [Fact]
    public async Task CreateProductFailNoPermission()
    {
        var (rsp, _) = await App.CustomerClient.PUTAsync<Update.Endpoint, Update.Request, Update.Response>(
                           new()
                           {
                               Name = "Grape Juice",
                               Description = "description",
                               ModifiedBy = "me",
                               Price = 100
                           });

        rsp.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateProductSuccess()
    {
        var (res, result) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, Create.Response>(
                                new()
                                {
                                    Name = "Grape Juice",
                                    Description = "description",
                                    ModifiedBy = "me",
                                    Price = 100
                                });

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ProductId.ShouldBeGreaterThan(1);
        result.ProductName.ShouldBe("Grape Juice");
    }

    [Fact]
    public async Task CreatedAtSuccess()
    {
        var (res, result) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, Create.Response>(
                                new()
                                {
                                    Name = "Grape Juice",
                                    Description = "description",
                                    ModifiedBy = "me",
                                    Price = 100,
                                    GenerateFullUrl = false
                                });

        var createdAtLocation = res.Headers.Location?.ToString();

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        createdAtLocation.ShouldBe($"/api/inventory/get-product/{result.ProductId}");
        result.ProductId.ShouldBeGreaterThan(1);
        result.ProductName.ShouldBe("Grape Juice");
    }

    [Fact]
    public async Task CreatedAtSuccessFullUrl()
    {
        var (res, result) = await App.AdminClient.POSTAsync<Create.Endpoint, Create.Request, Create.Response>(
                                new()
                                {
                                    Name = "Grape Juice",
                                    Description = "description",
                                    ModifiedBy = "me",
                                    Price = 100,
                                    GenerateFullUrl = true
                                });

        var createdAtLocation = res.Headers.Location?.ToString();

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        createdAtLocation.ShouldBe($"http://localhost/api/inventory/get-product/{result.ProductId}");
        result.ProductId.ShouldBeGreaterThan(1);
        result.ProductName.ShouldBe("Grape Juice");
    }

    [Fact]
    public async Task ResponseCaching()
    {
        var (rsp1, res1) = await App.GuestClient.GETAsync<GetProduct.Endpoint, GetProduct.Response>();

        rsp1.StatusCode.ShouldBe(HttpStatusCode.OK);

        await Task.Delay(100, Cancellation);

        var (rsp2, res2) = await App.GuestClient.GETAsync<GetProduct.Endpoint, GetProduct.Response>();

        rsp2.StatusCode.ShouldBe(HttpStatusCode.OK);
        res2.LastModified.ShouldBe(res1.LastModified);
    }

    [Fact]
    public async Task DeleteProductSuccess()
    {
        var res = await App.AdminClient.DELETEAsync<Delete.Endpoint, Delete.Request>(
                      new()
                      {
                          ItemID = Guid.NewGuid().ToString()
                      });

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}