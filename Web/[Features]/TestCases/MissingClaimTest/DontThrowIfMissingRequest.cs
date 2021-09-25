using FastEndpoints;
using Web.Auth;

namespace TestCases.MissingClaimTest
{
    public class DontThrowIfMissingRequest : IRequest
    {
        [From(Claim.NullClaim, forbidIfMissing: false)]
        public string? TestProp { get; set; }
    }
}