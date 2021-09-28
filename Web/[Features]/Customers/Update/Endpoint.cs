using FastEndpoints;
using Web.Auth;

namespace Customers.Update
{
    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Verbs(Http.PUT);
            Routes("/customers/update");
            Permissions(Allow.Customers_Update);
        }

        protected override Task HandleAsync(Request req, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
