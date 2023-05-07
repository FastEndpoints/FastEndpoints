using TestCases.EventHandlingTest;

namespace Sales.Orders.Create;

public class Request
{
    public int CustomerID { get; set; }
    public int ProductID { get; set; }
    public int Quantity { get; set; }

    /// <summary>
    /// this is a guid property description
    /// </summary>
    public Guid GuidTest { get; set; }
}

public class Response
{
    public int OrderID { get; set; }
    public string? Message { get; set; }
    public string? AnotherMsg { get; set; }
    public Guid GuidTest { get; set; }
    public SomeEvent Event { get; set; }
}
