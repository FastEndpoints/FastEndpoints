using EZEndpoints;
using EZEndpoints.Security;
using Web.Auth;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.GET, Http.POST);
            Routes("/test/{id}");
            //Roles("Admin");
            //Policies("AdminOnly");
            //Permissions(
            //    Allow.Inventory_Create_Item,
            //    Allow.Inventory_Retrieve_Item,
            //    Allow.Inventory_Update_Item);
            AllowAnnonymous();
            //DontThrowIfValidationFails();
        }

        protected override Task HandleAsync(Request req, Context<Request> ctx)
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
