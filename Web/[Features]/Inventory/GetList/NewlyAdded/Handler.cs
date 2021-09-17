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

        protected override Task HandleAsync(Request req, HttpContext ctx)
        {
            throw new NotImplementedException();
        }
    }
}
