using EZEndpoints;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Routes("/test/{id}");
            Verbs(Http.GET, Http.POST);
            AllowAnnonymous();
            //Roles("Admin");
            //Policies("AdminOnly");
            //Permissions(
            //    Allow.Inventory_Create_Item,
            //    Allow.Inventory_Retrieve_Item,
            //    Allow.Inventory_Update_Item);
            //DontThrowIfValidationFails();
        }

        protected override Task ExecuteAsync(Request req, Context<Request> ctx)
        {
            var res = new Response
            {
                Message = req.Id.ToString(),
                Name = req.Name,
                Price = req.Price
            };

            return ctx.SendAsync(res);
        }
    }
}
