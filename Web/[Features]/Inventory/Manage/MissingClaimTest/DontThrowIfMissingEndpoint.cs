using FastEndpoints;

namespace Inventory.Manage.MissingClaimTest
{
    public class DontThrowIfMissingEndpoint : Endpoint<DontThrowIfMissingRequest>
    {
        public DontThrowIfMissingEndpoint()
        {
            Verbs(Http.POST);
            Routes("/inventory/manage/missing-claim-test/dont-throw");
        }

        protected override Task ExecuteAsync(DontThrowIfMissingRequest req, CancellationToken ct)
        {
            return SendAsync($"you sent {req.TestProp}");
        }
    }
}