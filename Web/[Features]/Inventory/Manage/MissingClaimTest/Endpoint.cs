using ApiExpress;

namespace Inventory.Manage.MissingClaimTest
{
    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/inventory/manage/missing-claim-test");
        }

        protected override Task ExecuteAsync(Request req, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}