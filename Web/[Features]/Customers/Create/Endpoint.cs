using System.Text.Json.Serialization;
using Web.Services;

namespace Customers.Create;

[JsonSerializable(typeof(Request))]
public partial class CustomerCreate : JsonSerializerContext { }

public class Request
{
    [From(Claim.UserName)]
    public string? CreatedBy { get; set; }

    /// <summary>
    /// the name of the cutomer goes here
    /// </summary>
    public string? CustomerName { get; set; }

    public IEnumerable<string> PhoneNumbers { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    public IEmailService? Emailer { get; set; }

    public override void Configure()
    {
        Verbs(Http.POST, Http.GET);
        Routes(
            "/customer/new/{RefererID}",
            "/customer/{cID}/new/{SourceID}",
            "/customer/save");
        SerializerContext<CustomerCreate>();
    }

    public override Task HandleAsync(Request r, CancellationToken t)
    {
        if (r.PhoneNumbers?.Count() < 2)
            ThrowError("Not enough phone numbers!");

        var msg = Emailer?.SendEmail() + " " + r.CreatedBy;

        return SendAsync(msg ?? "emailer not resolved!");
    }
}
