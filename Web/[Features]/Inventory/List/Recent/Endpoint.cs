using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace Inventory.List.Recent;

[HttpGet("/inventory/list/recent/{CategoryID}")]
[Authorize(
    Roles = "Admin,TestRole",
    Policy = "AdminOnly",
    AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class Endpoint : EndpointWithoutRequest<Response>
{
    public override Task HandleAsync(CancellationToken t)
    {
        Response.Category = HttpContext.GetRouteValue("CategoryID")?.ToString();
        return SendAsync(Response);
    }
}
