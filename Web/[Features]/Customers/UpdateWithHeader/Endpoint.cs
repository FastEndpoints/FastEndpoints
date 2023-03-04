namespace Customers.UpdateWithHeader;

public record Request(
    [property: FromHeader] int CustomerID,
    [property: FromHeader("tenant-id")] string TenantID,
    string Name,
    int Age,
    string Address);

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