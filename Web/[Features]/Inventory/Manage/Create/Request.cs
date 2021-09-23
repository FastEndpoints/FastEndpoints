using ApiExpress;
using Web.Auth;

namespace Inventory.Manage.Create
{
    public class Request : ProductModel, IRequest
    {
        [From(Claim.UserID)]
        public string? UserID { get; set; }
    }
}