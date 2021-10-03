using FastEndpoints;
using Web.Auth;

namespace Customers.Update
{
#pragma warning disable CS8618
    public class Request
    {
        [From(Claim.CustomerID, IsRequired = false)] //allow non customers to set the customer id for updates
        public string CustomerID { get; set; }

        public string Name { get; set; }
        public int Age { get; set; }
        public string Address { get; set; }
    }
#pragma warning restore CS8618

    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Verbs(Http.PUT);
            Routes("/customers/update");
            Claims(allowAny: true,
                Claim.AdminID,
                Claim.CustomerID);
            Permissions(Allow.Customers_Update);
        }

        protected override Task HandleAsync(Request req, CancellationToken ct)
        {
            return SendAsync(req.CustomerID);
        }
    }
}
