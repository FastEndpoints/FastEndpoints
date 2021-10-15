namespace Customers.Update;

#pragma warning disable CS8618
public class Request
{
    [From(Claim.CustomerID, IsRequired = false)] //allow non customers to set the customer id for updates
    public string CustomerID { get; set; }

    public string Name { get; set; }
    public int Age { get; set; }
    public string Address { get; set; }
}
#pragma warning restore CS8618

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.PUT);
        Routes(
            "/customers/update",
            "/customer/save");
        Claims(allowAny: true,
            Claim.AdminID,
            Claim.CustomerID);
        Permissions(Allow.Customers_Update);
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        if (!User.HasPermission(Allow.Customers_Update))
            ThrowError("no permission!");

        return SendAsync(req.CustomerID);
    }
}

