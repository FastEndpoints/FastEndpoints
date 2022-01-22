namespace Admin.Login;

public class Endpoint : Endpoint<Request, Response>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("admin/login");
        AllowAnonymous();
        Options(b => b.RequireCors(b => b.AllowAnyOrigin()));
        Describe(b => b.Accepts<Request>("application/json"));
    }

    public override Task HandleAsync(Request r, CancellationToken ct)
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
                    Allow.Customers_Create,
                    Allow.Customers_Update};

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

public class Endpoint_V1 : Endpoint
{
    public override void Configure()
    {
        base.Configure();
        Version("1");
    }
}

public class Endpoint_V2 : Endpoint
{
    public override void Configure()
    {
        base.Configure();
        Version("2");
    }
}