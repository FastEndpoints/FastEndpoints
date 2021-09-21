using EZEndpoints;

namespace Inventory.GetList.NewlyAdded
{
    public class Request : IRequest
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Price { get; set; }
    }
}
