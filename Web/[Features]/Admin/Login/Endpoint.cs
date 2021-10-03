using FastEndpoints;
using FastEndpoints.Security;
using FastEndpoints.Validation;
using Web.Auth;

namespace Admin.Login
{
    public class Request
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
    }

    public class Validator : Validator<Request>
    {
        public Validator()
        {
            RuleFor(x => x.UserName)
                .NotEmpty().WithMessage("Username is required!")
                .MinimumLength(3).WithMessage("Username too short!");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required!")
                .MinimumLength(3).WithMessage("Password too short!");
        }
    }

    public class Endpoint : Endpoint<Request>
    {
        public Endpoint()
        {
            Verbs(Http.POST);
            Routes("/admin/login");
            AllowAnnonymous();
            Options(b => b.RequireCors(b => b.AllowAnyOrigin()));
        }

        protected override Task HandleAsync(Request r, CancellationToken ct)
        {
            if (r.UserName == "admin" && r.Password == "pass")
            {
                var expiryDate = DateTime.UtcNow.AddDays(1);

                var userPermissions = new[] {
                    Allow.Inventory_Create_Item,
                    Allow.Inventory_Retrieve_Item,
                    Allow.Inventory_Update_Item,
                    Allow.Inventory_Delete_Item,
                    Allow.Customers_Retrieve,
                    Allow.Customers_Create};

                var userClaims = new[] {
                    (Claim.UserName, r.UserName),
                    (Claim.UserType, Role.Admin),
                    (Claim.AdminID, "USR0001") };

                var userRoles = new[] {
                    Role.Admin,
                    Role.Staff };

                var token = JWTBearer.CreateToken(
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

            return SendErrorsAsync();
        }
    }

    public class Response
    {
        public string? JWTToken { get; set; }
        public DateTime ExpiryDate { get; set; }
        public IEnumerable<string>? Permissions { get; set; }
    }
}
