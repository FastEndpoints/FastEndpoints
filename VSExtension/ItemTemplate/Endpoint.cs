using FastEndpoints;

namespace $fileinputname$
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/route/path/here");
        }

        protected override Task ExecuteAsync(Request req, CancellationToken ct)
        {
            return SendAsync("This endpoint is not implemented!");
        }
    }
}