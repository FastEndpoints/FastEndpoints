using FastEndpoints;
using Web.Auth;

namespace TestCases.MissingClaimTest
{
    public class DontThrowIfMissingRequest
    {
        [From(Claim.NullClaim, forbidIfMissing: false)]
        public string? TestProp { get; set; }
    }
}