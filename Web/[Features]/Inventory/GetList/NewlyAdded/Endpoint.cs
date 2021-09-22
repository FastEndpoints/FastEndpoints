using ApiExpress;
using ApiExpress.Security;
using Web.Auth;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Endpoint<Request, Validator>
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

        public ILogger<Endpoint>? MyLoggerService { get; set; }

        protected override Task ExecuteAsync(Request req, CancellationToken ct)
        {
            //var token = JWTBearer.CreateTokenWithClaims(
            //    Config["TokenKey"],
            //    null,
            //    null,
            //    null,
            //    (Claim.UserName, "damith gunner"));

            var res = new Response
            {
                Message = req.UserName,
                Name = req.Name,
                Price = req.Price
            };

            return SendAsync(res);
        }
    }
}
