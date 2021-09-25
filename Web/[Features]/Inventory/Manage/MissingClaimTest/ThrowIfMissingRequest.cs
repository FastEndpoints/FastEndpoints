using FastEndpoints;
using Web.Auth;

namespace Inventory.Manage.MissingClaimTest
{
    public class ThrowIfMissingRequest : IRequest
    {
        [From(Claim.NullClaim)]
        public string? TestProp { get; set; }
    }
}