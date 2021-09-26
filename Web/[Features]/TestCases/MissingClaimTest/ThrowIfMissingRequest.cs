using FastEndpoints;
using Web.Auth;

namespace TestCases.MissingClaimTest
{
    public class ThrowIfMissingRequest
    {
        [From(Claim.NullClaim)]
        public string? TestProp { get; set; }
    }
}