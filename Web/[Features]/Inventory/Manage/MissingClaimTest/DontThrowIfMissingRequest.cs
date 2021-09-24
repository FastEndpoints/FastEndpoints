using ApiExpress;
using Web.Auth;

namespace Inventory.Manage.MissingClaimTest
{
    public class DontThrowIfMissingRequest : IRequest
    {
        [From(Claim.NullClaim, forbidIfMissing: false)]
        public string? TestProp { get; set; }
    }
}