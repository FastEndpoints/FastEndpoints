using Microsoft.AspNetCore.Authorization;

namespace TestCases.IAuthorizationServiceInjectionTest;

sealed class Endpoint : EndpointWithoutRequest<bool>
{
    readonly IAuthorizationService _auth;

    public Endpoint(IAuthorizationService auth)
    {
        _auth = auth;
    }

    public override void Configure()
    {
        Get("/tests/iauth-injection");
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        var res = await _auth.AuthorizeAsync(User, "AdminOnly");
        await SendAsync(res.Succeeded);
    }
}
