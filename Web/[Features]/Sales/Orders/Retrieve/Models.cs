namespace Sales.Orders.Retrieve;

public class Request
{
    public string OrderID { get; set; }
}

public class Response
{
    public string Message => "ok!";
}
