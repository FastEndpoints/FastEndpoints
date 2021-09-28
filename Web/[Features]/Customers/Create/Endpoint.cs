using FastEndpoints;
using Web.Services;

namespace Customers.Create
{
    public class Endpoint : Endpoint<Request>
    {
        public IEmailService? Emailer { get; set; }

        public Endpoint()
        {
            Verbs(Http.POST);
            Routes(
                "/customers/new/{RefererID}",
                "/customers/create");
            AllowAnnonymous();
        }

        protected override Task HandleAsync(Request r, CancellationToken t)
        {
            var msg = Emailer?.SendEmail() + " " + r.CreatedBy;

            return SendAsync(msg ?? "emailer not resolved!");
        }
    }
}