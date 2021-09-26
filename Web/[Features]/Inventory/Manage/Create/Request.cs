using FastEndpoints;
using Web.Auth;

namespace Inventory.Manage.Create
{
    public class Request : ProductModel
    {
        [From(Claim.UserID)]
        public string? UserID { get; set; }
    }
}