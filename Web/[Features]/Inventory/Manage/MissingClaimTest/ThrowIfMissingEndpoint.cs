using FastEndpoints;

namespace Inventory.Manage.MissingClaimTest
{
    public class ThrowIfMissingEndpoint : Endpoint<ThrowIfMissingRequest>
    {
        public ThrowIfMissingEndpoint()
        {
            Verbs(Http.POST);
            Routes("/inventory/manage/missing-claim-test");
        }

        protected override Task ExecuteAsync(ThrowIfMissingRequest req, CancellationToken ct)
        {
            //this line will never be reached as ErrorResponse will be sent due to claim missing
            return Task.CompletedTask;
        }
    }
}