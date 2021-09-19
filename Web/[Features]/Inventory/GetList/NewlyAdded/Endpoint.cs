using ASPie;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.GET, Http.POST);
            Routes("/test/{id}");
            AllowAnnonymous();
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
