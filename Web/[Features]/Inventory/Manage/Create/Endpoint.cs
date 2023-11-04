﻿using System.Security.Claims;
using TestCases.EventHandlingTest;
using Claim = Web.Auth.Claim;

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
        AccessControl("Inventory_Create_Item", "Admin");
        ClaimsAll(
            Claim.AdminID,
            "test-claim");
        Policy(b => b.RequireRole(Role.Admin));
        Description(
            x => x.Accepts<Request>("application/json")
                  .Produces(201)
                  .Produces(500)
                  .WithTags("test")
                  .WithName("CreateInventoryItem"),
            clearDefaults: true);
        Summary(
            s =>
            {
                s.ExampleRequest = new()
                {
                    Description = "first description",
                    Price = 10,
                    QtyOnHand = 10,
                    Id = 1,
                    Name = "first name",
                    Username = "blah"
                };
                s.ExampleRequest = new()
                {
                    Description = "description two",
                    Price = 209,
                    QtyOnHand = 290,
                    Id = 200,
                    Name = "second  name"
                };
            });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var validation = ValidationContext<Request>.Instance;

        if (string.IsNullOrEmpty(req.Description))
            AddError(x => x.Description, "Please enter a product descriptions!");

        if (req.Price > 1000)
        {
            //AddError(x => x.Price, "Price is too high!");
            validation.AddError(x => x.Price, "Price is too high!");
        }

        var eventdto = new NewItemAddedToStock
        {
            Name = req.Name,
            Quantity = 1
        };
        await eventdto.PublishAsync();

        if (eventdto.Name != "pass" && HttpContext.Response.Body is null) //response body is null in a unit test
            AddError("event publish failed!");

        ThrowIfAnyErrors();

        if (req.Name == "Apple Juice")
        {
            //ThrowError("Product already exists!");
            validation.ThrowError("Product already exists!");
        }

        var res = new Response
        {
            ProductId = new Random().Next(1, 1000),
            ProductName = req.Name
        };

        await SendCreatedAtAsync<GetProduct.Endpoint>(
            routeValues: new { ProductID = res.ProductId },
            responseBody: res,
            generateAbsoluteUrl: req.GenerateFullUrl);
    }
}