using FastEndpoints;
using Web.Auth;

namespace TestCases.MissingClaimTest
{
    public class DontThrowIfMissingRequest
    {
        [From(Claim.NullClaim, isRequired: false)]
        public string? TestProp { get; set; }
    }
}