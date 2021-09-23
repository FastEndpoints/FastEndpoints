using ApiExpress;

namespace Inventory.Manage.Create
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/inventory/manage/create");
        }

        protected override Task ExecuteAsync(Request req, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
