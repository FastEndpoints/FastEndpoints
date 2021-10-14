using Web.Services;

namespace Customers.Create;

public class Request
{
    [From(Claim.UserName)] public string? CreatedBy { get; set; }
    public string? CustomerName { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    public IEmailService? Emailer { get; set; }

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes(
            "/customers/new/{RefererID}",
            "/customers/create");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken t)
    {
        var msg = Emailer?.SendEmail() + " " + r.CreatedBy;

        return SendAsync(msg ?? "emailer not resolved!");
    }
}
