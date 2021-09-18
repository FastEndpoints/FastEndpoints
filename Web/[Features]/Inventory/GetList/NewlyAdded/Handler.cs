using ASPie;

namespace Inventory.GetList.NewlyAdded
{
    public class Handler : HandlerBase<Request, Response, Validator>
    {
        public Handler()
        {
            Verbs(Http.GET);
            Routes("/test/{id}");
            ThrowIfValidationFails(true);
        }

        protected override Task HandleAsync(Request req, RequestContext ctx)
        {
            return Task.CompletedTask;
        }
    }
}
