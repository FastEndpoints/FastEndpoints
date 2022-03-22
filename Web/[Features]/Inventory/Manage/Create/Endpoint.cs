namespace Inventory.Manage.Create;

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Post("/inventory/manage/create");
        Policies("AdminOnly");
        Permissions(
            Allow.Inventory_Create_Item,
            Allow.Inventory_Update_Item);
        ClaimsAll(
            Claim.AdminID,
            "test-claim");
        Description(x => x
            .Accepts<Request>("application/json")
            .Produces(201)
            .Produces(500)
            .WithTags("test")
            .WithName("CreateInventoryItem"),
            clearDefaults: true);
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(req.Description))
            AddError(x => x.Description!, "Please enter a product descriptions!");

        if (req.Price > 1000)
            AddError(x => x.Price, "Price is too high!");

        ThrowIfAnyErrors();

        if (req.Name == "Apple Juice")
            ThrowError("Product already exists!");

        var res = new Response
        {
            ProductId = new Random().Next(1, 1000),
            ProductName = req.Name
        };

        return SendCreatedAtAsync<GetProduct.Endpoint>(
            routeValues: new { ProductID = res.ProductId },
            responseBody: res,
            generateAbsoluteUrl: req.GenerateFullUrl);
    }
}
