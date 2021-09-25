using FastEndpoints;

namespace TestCases.MissingClaimTest
{
    public class DontThrowIfMissingEndpoint : Endpoint<DontThrowIfMissingRequest>
    {
        public DontThrowIfMissingEndpoint()
        {
            Verbs(Http.POST);
            Routes("/test-cases/missing-claim-test/dont-throw");
        }

        protected override Task ExecuteAsync(DontThrowIfMissingRequest req, CancellationToken ct)
        {
            return SendAsync($"you sent {req.TestProp}");
        }
    }
}