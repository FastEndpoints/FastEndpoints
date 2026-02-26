namespace Web.SystemEvents;

public class NewOrderCreated
{
    public int OrderID { get; set; }
    public string? CustomerName { get; set; }
    public decimal OrderTotal { get; set; }
}