using Microsoft.AspNetCore.Authorization;

namespace Customers.Login;

[HttpGet("/customer/login")]
[AllowAnonymous]
public class Endpoint : EndpointWithoutRequest
{
    public override Task HandleAsync(CancellationToken t)
    {
        var token = JWTBearer.CreateToken(
            signingKey: Config!["TokenKey"],
            permissions: new[] { Allow.Customers_Create, Allow.Customers_Update, Allow.Customers_Retrieve },
            roles: new[] { Role.Customer },
            claims: new[] { (Claim.CustomerID, "CST001") });

        return SendAsync(token);
    }
}
