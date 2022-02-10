namespace Customers.Update;

public class Request
{
    [FromClaim(Claim.CustomerID, IsRequired = false)] //allow non customers to set the customer id for updates
    public string CustomerID { get; set; }

    public string Name { get; set; }
    public int Age { get; set; }
    public string Address { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.PUT);
        Routes(
            "/customer/update",
            "/customer/save");
        Claims(
            Claim.AdminID,
            Claim.CustomerID);
        PermissionsAll(
            Allow.Customers_Create,
            Allow.Customers_Update,
            Allow.Customers_Retrieve);
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        if (!User.HasPermission(Allow.Customers_Update))
            ThrowError("no permission!");

        return SendAsync(req.CustomerID);
    }
}