namespace Sales.Orders.Retrieve;

public class Request
{
    [FromHeader("tenant-id", IsRequired = false)]
    public string TenantID { get; set; }

    public string OrderID { get; set; }
}

public class Response
{
    public string Message => "ok!";
}
