using ApiExpress;

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
            throw new NotImplementedException();
        }
    }
}