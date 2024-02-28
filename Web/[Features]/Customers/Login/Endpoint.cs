using Microsoft.AspNetCore.Authorization;

namespace Customers.Login;

[HttpGet("/customer/login"), AllowAnonymous]
public class Endpoint : EndpointWithoutRequest
{
    public override Task HandleAsync(CancellationToken t)
    {
        var token = JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = Config["TokenKey"]!;
                o.User.Permissions.AddRange(
                [
                    Allow.Customers_Create,
                    Allow.Customers_Update,
                    Allow.Customers_Retrieve,
                    Allow.Sales_Order_Create
                ]);
                o.User.Roles.Add(Role.Customer);
                o.User[Claim.CustomerID] = "CST001";
            });

        return SendAsync(token);
    }
}