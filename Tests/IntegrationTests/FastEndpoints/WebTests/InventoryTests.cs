using IntegrationTests.Shared.Fixtures;
using System.Net;
using Xunit;
using Xunit.Abstractions;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace FastEndpoints.IntegrationTests.WebTests;

public class InventoryTests : EndToEndTestBase
{
    public InventoryTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) :
        base(endToEndTestFixture, outputHelper)
    {
    }

    [Fact]
    public async Task CreateProductFailValidation()
    {
        var (res, result) = await AdminClient.POSTAsync<
            Inventory.Manage.Create.Endpoint,
            Inventory.Manage.Create.Request,
            ErrorResponse>(new()
            {
                Price = 1100
            });

        res?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result?.Errors.Count.Should().Be(2);
        result?.Errors.Should().ContainKey("Name");
        result?.Errors.Should().ContainKey("ModifiedBy");
    }

    [Fact]
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

        res?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result?.Errors.Should().NotBeNull();
        result?.Errors.Count.Should().Be(2);
        result?.Errors.Should().ContainKey("Description");
        result?.Errors.Should().ContainKey("Price");
    }

    [Fact]
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

        res?.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result?.Errors.Should().NotBeNull();
        result?.Errors.Count.Should().Be(1);
        result?.Errors.Should().ContainKey("GeneralErrors");
    }

    [Fact]
    public async Task CreateProductFailNoPermission()
    {
        var (rsp, _) = await CustomerClient.PUTAsync<
            Inventory.Manage.Update.Endpoint,
            Inventory.Manage.Update.Request,
            Inventory.Manage.Update.Response>(new()
            {
                Name = "Grape Juice",
                Description = "description",
                ModifiedBy = "me",
                Price = 100
            });

        rsp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
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

        res?.StatusCode.Should().Be(HttpStatusCode.Created);
        result?.ProductId.Should().BeGreaterThan(1);
        result?.ProductName.Should().Be("Grape Juice");
    }

    [Fact]
    public async Task CreatedAtSuccess()
    {
        var (res, result) = await AdminClient.POSTAsync<
            Inventory.Manage.Create.Endpoint,
            Inventory.Manage.Create.Request,
            Inventory.Manage.Create.Response>(new()
            {
                Name = "Grape Juice",
                Description = "description",
                ModifiedBy = "me",
                Price = 100,
                GenerateFullUrl = false
            });

        var createdAtLocation = res?.Headers.Location?.ToString();

        res?.StatusCode.Should().Be(HttpStatusCode.Created);
        createdAtLocation.Should().Be($"/api/inventory/get-product/{result?.ProductId}");
        result?.ProductId.Should().BeGreaterThan(1);
        result?.ProductName.Should().Be("Grape Juice");
    }

    [Fact]
    public async Task CreatedAtSuccessFullUrl()
    {
        var (res, result) = await AdminClient.POSTAsync<
            Inventory.Manage.Create.Endpoint,
            Inventory.Manage.Create.Request,
            Inventory.Manage.Create.Response>(new()
            {
                Name = "Grape Juice",
                Description = "description",
                ModifiedBy = "me",
                Price = 100,
                GenerateFullUrl = true
            });

        var createdAtLocation = res?.Headers.Location?.ToString();

        res?.StatusCode.Should().Be(HttpStatusCode.Created);
        createdAtLocation.Should().Be($"http://localhost/api/inventory/get-product/{result?.ProductId}");
        result?.ProductId.Should().BeGreaterThan(1);
        result?.ProductName.Should().Be("Grape Juice");
    }

    [Fact]
    public async Task ResponseCaching()
    {
        var (rsp1, res1) =
            await GuestClient.GETAsync<Inventory.GetProduct.Endpoint, Inventory.GetProduct.Response>();
        Assert.AreEqual(HttpStatusCode.OK, rsp1?.StatusCode);

        await Task.Delay(100);

        var (rsp2, res2) =
            await GuestClient.GETAsync<Inventory.GetProduct.Endpoint, Inventory.GetProduct.Response>();

        rsp2?.StatusCode.Should().Be(HttpStatusCode.OK);
        res2?.LastModified.Should().Be(res1?.LastModified);
    }

    [Fact]
    public async Task DeleteProductSuccess()
    {
        var res = await AdminClient.DELETEAsync<
            Inventory.Manage.Delete.Endpoint,
            Inventory.Manage.Delete.Request>(new()
            {
                ItemID = Guid.NewGuid().ToString()
            });

        res?.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}