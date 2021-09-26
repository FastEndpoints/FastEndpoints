using FastEndpoints;
using Web.Auth;

#pragma warning disable CS8618
namespace Customers.Create
{
    public class Request
    {
        [From(Claim.UserName)]
        public string CreatedBy { get; set; }

        public string CustomerName { get; set; }
    }
}