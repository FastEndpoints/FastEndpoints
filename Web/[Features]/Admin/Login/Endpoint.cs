using ApiExpress;

namespace Admin.Login
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/admin/login");
            AllowAnnonymous();
        }

        protected override Task ExecuteAsync(Request req, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
