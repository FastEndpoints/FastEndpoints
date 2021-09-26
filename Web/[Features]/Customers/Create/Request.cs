#pragma warning disable CS8618

using FastEndpoints;
using Web.Auth;

namespace Customers.Create
{
    public class Request
    {
        [From(Claim.UserName)]
        public string CreatedBy { get; set; }

        public string CustomerName { get; set; }
    }
}