using ApiExpress;
using Web.Auth;

namespace Inventory.Manage.MissingClaimTest
{
    public class Request : IRequest
    {
        [From(Claim.NullClaim)]
        public string? TestProp { get; set; }
    }
}