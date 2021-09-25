using FastEndpoints;
using FastEndpoints.Security;
using Web.Auth;

namespace Admin.Login
{
    public class Endpoint : Endpoint<Request, Validator>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/admin/login");
            AllowAnnonymous();
        }

        protected override Task ExecuteAsync(Request r, CancellationToken ct)
        {
            if (r.UserName == "admin" && r.Password == "pass")
            {
                var expiryDate = DateTime.UtcNow.AddDays(1);

                var userPermissions = new[] {
                    Allow.Customers_Retrieve_Recent,
                    Allow.Inventory_Create_Item,
                    Allow.Inventory_Retrieve_Item,
                    Allow.Inventory_Update_Item,
                    Allow.Inventory_Delete_Item };

                var userClaims = new[] {
                    (Claim.UserName, r.UserName),
                    (Claim.UserType, Role.Admin),
                    (Claim.UserID, "USR0001") };

                var userRoles = new[] {
                    Role.Admin,
                    Role.Staff };

                var token = JWTBearer.CreateTokenWithClaims(
                    Config["TokenKey"],
                    expiryDate,
                    userPermissions,
                    userRoles,
                    userClaims);

                return SendAsync(new Response()
                {
                    JWTToken = token,
                    ExpiryDate = expiryDate,
                    Permissions = new Allow().NamesFor(userPermissions)
                });
            }
            else
            {
                AddError("Authentication Failed!");
            }

            return SendErrorAsync();
        }
    }
}
