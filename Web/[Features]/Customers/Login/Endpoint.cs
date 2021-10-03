using FastEndpoints;
using FastEndpoints.Security;
using Web.Auth;

namespace Customers.Login
{
    public class Endpoint : EndpointWithoutRequest
    {
        public Endpoint()
        {
            Verbs(Http.GET);
            Routes("/customers/login");
            AllowAnnonymous();
        }

        protected override Task HandleAsync(EmptyRequest r, CancellationToken t)
        {
            var token = JWTBearer.CreateToken(
                signingKey: Config["TokenKey"],
                permissions: new[] { Allow.Customers_Create, Allow.Customers_Update, Allow.Customers_Retrieve },
                roles: new[] { Role.Customer },
                claims: new[] { (Claim.CustomerID, "CST001") });

            return SendAsync(token);
        }
    }
}