using FastEndpoints;
using Web.Auth;

namespace Customers.List.Recent
{
    public class Response
    {
        public IEnumerable<KeyValuePair<string, int>>? Customers { get; set; }
    }

    public class Endpoint : BasicEndpoint
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/customers/list/recent");
            Policies("AdminOnly");
            Roles(
                Role.Admin,
                Role.Staff);
            Permissions(
                Allow.Customers_Retrieve,
                Allow.Customers_Create);
        }

        protected override Task HandleAsync(EmptyRequest er, CancellationToken ct)
        {
            return SendAsync(new Response
            {
                Customers = new[] {
                    new KeyValuePair<string,int>("ryan gunner", 123),
                    new KeyValuePair<string,int>("debby ryan", 124),
                    new KeyValuePair<string,int>("ryan reynolds",321)
                }
            });
        }
    }
}
