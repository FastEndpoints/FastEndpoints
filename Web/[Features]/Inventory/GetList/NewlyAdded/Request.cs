using EZEndpoints;

namespace Inventory.GetList.NewlyAdded
{
    public class Request : IRequest
    {
        public string? Name { get; set; }
        public int Price { get; set; }
    }
}
