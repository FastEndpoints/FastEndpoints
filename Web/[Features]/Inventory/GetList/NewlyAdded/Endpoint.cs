using ASPie;

namespace Inventory.GetList.NewlyAdded
{
    public class Endpoint : Handler<Request, Response, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/test/{id}");
            DontThrowIfValidationFails();
        }

        protected override Task HandleAsync(Request req, Context<Request> ctx)
        {
            ctx.AddError(r => r.Name, "name error");

            ctx.ThrowIfAnyErrors();

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
