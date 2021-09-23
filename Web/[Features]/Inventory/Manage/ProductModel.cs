namespace Inventory.Manage
{
    public class ProductModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int QtyOnHand { get; set; }
        public string? ModifiedBy { get; set; }
    }
}
