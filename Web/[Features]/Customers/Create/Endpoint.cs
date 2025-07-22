using Web.Services;

namespace Customers.Create;

public class Request
{
    public string cID { get; set; }

    [From(Claim.UserName)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// the name of the cutomer goes here
    /// </summary>
    public string? CustomerName { get; set; }

    public IEnumerable<string> PhoneNumbers { get; set; }

    [HasPermission(Allow.Customers_Create)]
    public bool HasCreatePermission { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    readonly IEmailService? _emailer;

    public Endpoint(IEmailService emailer)
    {
        _emailer = emailer;
    }

    public override void Configure()
    {
        Verbs(Http.POST, Http.GET);
        Routes(
            "/customer/new/{RefererID}",
            "/customer/{cID}/new/{SourceID}",
            "/customer/save");
        Permissions(Allow.Customers_Create);
        AccessControl( // Permission for creating a new customer in the system.
            "Customers+Create",
            Apply.ToThisEndpoint,
            "Admin",
            "Manager");
        DontAutoTag();
        Description(x => x.WithTags("Customer Save"));
    }

    public override async Task HandleAsync(Request r, CancellationToken t)
    {
        Logger.LogInformation("customer creation has begun!");

        if (r.PhoneNumbers?.Count() < 2)
            ThrowError("Not enough phone numbers!");

        var msg = _emailer?.SendEmail() + " " + r.CreatedBy;

        await Send.OkAsync(msg ?? "emailer not resolved!");
    }
}