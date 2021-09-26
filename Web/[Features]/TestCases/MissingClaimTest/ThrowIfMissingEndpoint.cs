using FastEndpoints;

namespace TestCases.MissingClaimTest
{
    public class ThrowIfMissingEndpoint : Endpoint<ThrowIfMissingRequest>
    {
        public ThrowIfMissingEndpoint()
        {
            Verbs(Http.POST);
            Routes("/test-cases/missing-claim-test");
        }

        protected override Task HandleAsync(ThrowIfMissingRequest req, CancellationToken ct)
        {
            //this line will never be reached as ErrorResponse will be sent due to claim missing
            return Task.CompletedTask;
        }
    }
}