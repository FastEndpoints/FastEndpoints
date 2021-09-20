using EZEndpoints;

namespace Inventory.GetList.NewlyAdded
{
    public class Response : IResponse
    {
        public int Price { get; set; }
        public string? Message { get; set; }
        public string? Name { get; internal set; }
    }
}
