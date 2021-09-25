using FastEndpoints;

namespace Customers.List.Recent
{
    public class Endpoint : BasicEndpoint
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/customers/list/recent");
        }

        protected override Task ExecuteAsync(EmptyRequest req, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
