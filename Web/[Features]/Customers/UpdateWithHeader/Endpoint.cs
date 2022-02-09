namespace Customers.UpdateWithHeader;

public class Request
{
    [FromHeader]
    public int CustomerID { get; set; }

    [FromHeader("tenant-id")]
    public string TenantID { get; set; }

    public string Name { get; set; }
    public int Age { get; set; }
    public string Address { get; set; }
}

public class Endpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Verbs(Http.PUT);
        Routes("/customer/update-with-header");
        Claims(
            Claim.AdminID,
            Claim.CustomerID);
        Permissions(
            Allow.Customers_Update);
    }

    public override Task HandleAsync(Request req, CancellationToken ct)
    {
        if (!User.HasPermission(Allow.Customers_Update))
            ThrowError("no permission!");

        return SendAsync(req.TenantID + "|" + req.CustomerID);
    }
}