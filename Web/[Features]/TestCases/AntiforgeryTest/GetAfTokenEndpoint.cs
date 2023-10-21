using Microsoft.AspNetCore.Antiforgery;

namespace TestCases.AntiforgeryTest;

public class TokenResponse
{
    public string TokenName { get; set; }
    public string? Value { get; set; }
}

public class GetAfTokenEndpoint : EndpointWithoutRequest<TokenResponse>
{
    readonly IAntiforgery _antiforgery;

    public GetAfTokenEndpoint(IAntiforgery antiforgery)
    {
        _antiforgery = antiforgery;
    }

    public override void Configure()
    {
        Get(AntiforgeryTest.Routes.GetToken);
        Tags("antiforgery");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var token = _antiforgery.GetAndStoreTokens(HttpContext!);
        await SendAsync(
            new()
            {
                TokenName = token.FormFieldName,
                Value = token.RequestToken
            });
    }
}