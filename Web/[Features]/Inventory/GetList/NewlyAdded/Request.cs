using ASPie;
using System.Reflection;

namespace Inventory.GetList.NewlyAdded
{
    public class Request : RequestBase<Request>
    {
        public string? Name { get; set; }
        public int Price { get; set; }

        public static ValueTask<Request?> BindAsync(HttpContext ctx, ParameterInfo _)
        {
            return ctx.Request.ReadFromJsonAsync<Request>();
        }
    }
}
