namespace Sales.Orders.Retrieve;

public class Request
{
    /// <summary>
    /// this is the tenant id
    /// </summary>
    [FromHeader("tenant-id", IsRequired = false)]
    public string TenantID { get; set; }

    public string OrderID { get; set; }

    [FromHeader("Content-Type")]
    public string ContentType { get; set; }
}

public class Response
{
    public string Message => "ok!";
}