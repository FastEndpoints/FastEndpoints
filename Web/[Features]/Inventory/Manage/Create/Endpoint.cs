using FastEndpoints;
using Web.Auth;

namespace Inventory.Manage.Create
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/inventory/manage/create");
            Permissions(allowAny: true,
                Allow.Inventory_Create_Item,
                Allow.Inventory_Update_Item);
        }

        protected override Task HandleAsync(Request req, CancellationToken ct)
        {
#pragma warning disable CS8603
            if (string.IsNullOrEmpty(req.Description))
                AddError(x => x.Description, "Please enter a product descriptions!");
#pragma warning restore CS8603

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

            return SendAsync(res);
        }
    }
}