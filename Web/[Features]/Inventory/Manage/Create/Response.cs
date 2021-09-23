using ApiExpress;

namespace Inventory.Manage.Create
{
    public class Response : IResponse
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
    }
}