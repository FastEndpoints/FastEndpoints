using ApiExpress;
using Web.Auth;

namespace Inventory.GetList.NewlyAdded
{
    public class Request : IRequest
    {
        [From(Claim.UserName)]
        public string? UserName { get; set; }

        public int Id { get; set; }
        public string? Name { get; set; }
        public int Price { get; set; }
    }
}
