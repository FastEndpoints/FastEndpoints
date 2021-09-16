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

        public override Task<Response> HandleAsync(Request r)
        {
            var res = NewResponse();
            res.Price = r.Price;
            res.Name = r.Name;
            res.Message = "All good...";
            return Task.FromResult(res);
        }
    }
}
