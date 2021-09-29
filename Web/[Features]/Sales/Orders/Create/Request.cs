namespace Sales.Orders.Create
{
    public class Request
    {
        public int CustomerID { get; set; }
        public int ProductID { get; set; }
        public int Quantity { get; set; }
    }
}