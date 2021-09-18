using ASPie;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Handler<Request, Response, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/test/{id}");
            //DontThrowIfValidationFails();
        }

        protected override Task HandleAsync(Request req, RequestContext ctx)
        {
            var res = new Response
            {
                Message = "success!",
                Name = req.Name,
                Price = req.Price
            };
            return ctx.SendAsync(res);
        }
    }
}
