using Web.Services;

namespace Customers.CreateWithPropertiesDI;

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
    public required IEmailService Emailer { get; init; }

    public override void Configure()
    {
        Verbs(Http.POST, Http.GET);
        Routes(
            "/customer/new/2{RefererID}",
            "/customer/{cID}/new2/{SourceID}",
            "/customer/save2");
        AccessControl("Customers_Create_2");
        DontAutoTag();
        Description(x => x.WithTags("Customer Save"));
    }

    public override async Task HandleAsync(Request r, CancellationToken t)
    {
        Logger.LogInformation("customer creation has begun!");

        if (r.PhoneNumbers?.Count() < 2)
            ThrowError("Not enough phone numbers!");

        var msg = Emailer.SendEmail() + " " + r.CreatedBy;

        await Send.OkAsync(msg ?? "emailer not resolved!");
    }
}