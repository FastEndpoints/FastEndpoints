using FastEndpoints;

namespace Customers.List.Recent
{
    public class Response : IResponse
    {
        public IEnumerable<KeyValuePair<string, int>>? Customers { get; set; }
    }
}
