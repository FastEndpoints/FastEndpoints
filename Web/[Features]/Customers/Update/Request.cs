#pragma warning disable CS8618
using FastEndpoints;
using Web.Auth;

namespace Customers.Update
{
    public class Request
    {
        [From(Claim.CustomerID, IsRequired = false)] //allow non customers to set the customer id for updates
        public string CustomerID { get; set; }

        public string Name { get; set; }
        public int Age { get; set; }
        public string Address { get; set; }
    }
}