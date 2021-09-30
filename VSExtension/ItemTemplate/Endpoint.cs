using FastEndpoints;

namespace $fileinputname$
{
    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/route/path/here");
        }

        protected override Task HandleAsync(Request r, CancellationToken t)
        {
            return SendAsync("This endpoint is not implemented yet!");
        }
    }
}