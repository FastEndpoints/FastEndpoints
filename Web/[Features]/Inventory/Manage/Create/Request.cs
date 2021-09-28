using FastEndpoints;
using Web.Auth;

namespace Inventory.Manage.Create
{
    public class Request : ProductModel
    {
        [From(Claim.AdminID)]
        public string? UserID { get; set; }
    }
}