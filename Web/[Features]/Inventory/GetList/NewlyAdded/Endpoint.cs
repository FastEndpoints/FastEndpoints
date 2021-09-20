using EZEndpoints;
using Web.Auth;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.GET, Http.POST);
            Routes("/test/{id}");
            Permissions(allowAny: true,
                Allow.Inventory_Create_Item,
                Allow.Inventory_Retrieve_Item,
                Allow.Inventory_Update_Item);

            //AllowAnnonymous();
            //DontThrowIfValidationFails();
        }

        protected override Task HandleAsync(Request req, Context<Request> ctx)
        {
            var res = new Response
            {
                Message = ctx.BaseURL,
                Name = req.Name,
                Price = req.Price
            };

            return ctx.SendAsync(res);
        }
    }
}
