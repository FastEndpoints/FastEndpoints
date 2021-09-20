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
            //AllowAnnonymous();
            //DontThrowIfValidationFails();
        }

        protected override Task HandleAsync(Request req, Context<Request> ctx)
        {
            var key = Config["TokenKey"];
            var token = JWTBearer.CreateTokenWithClaims(
                key,
                DateTime.Now.AddDays(1),
                new[] { Allow.Inventory_Retrieve_Item, Allow.Inventory_Create_Item, Allow.Inventory_Update_Item },
                new[] { "Admin", "Manager" },
                ("TestClaimType", "TestClaimValue"));

            var res = new Response
            {
                Message = token,
                Name = req.Name,
                Price = req.Price
            };

            return ctx.SendAsync(res);
        }
    }
}
