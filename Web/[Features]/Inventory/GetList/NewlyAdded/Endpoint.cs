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
            Permissions(allowAny: true,
                Allow.Inventory_Create_Item,
                Allow.Inventory_Retrieve_Item,
                Allow.Inventory_Update_Item);

            AllowAnnonymous();
            //DontThrowIfValidationFails();
        }

        protected override Task HandleAsync(Request req, Context<Request> ctx)
        {
            var key = Config["TokenKey"];
            var env = Env.EnvironmentName;
            Logger.LogWarning("this is a test warning");

            var logger = Resolve<ILogger<Endpoint>>();
            logger?.LogInformation("test from endpoint logger");


            //JWTBearer.CreateToken()

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
