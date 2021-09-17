using ASPie;

namespace Inventory.GetList.NewlyAdded
{
    public class Request : RequestBase<Request>
    {
        public string? Name { get; set; }
        public int Price { get; set; }
    }
}
