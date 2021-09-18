using ASPie;

namespace Inventory.GetList.NewlyAdded
{
    public class Handler : HandlerBase<Request, Response>
    {
        public Handler()
        {
            Verbs(Http.GET);
            Routes("/test/{id}");
        }

        protected override Task HandleAsync(Request req, RequestContext ctx)
        {
            return ctx.HttpContext.Response.WriteAsJsonAsync(new Response
            {
                Message = "all good!",
                Name = req.Name,
                Price = req.Price
            });
        }
    }
}
