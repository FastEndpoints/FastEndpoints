using FastEndpoints.Security;

namespace NativeAotChecker.Endpoints.Binding;

public class GetJwtTokenEndpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("jwt-token");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var token = JwtBearer.CreateToken(
            o =>
            {
                o.SigningKey = Config["Jwt-Secret"]!;
                o.User["user-id"] = "0001";
            });
        await Send.OkAsync(token);
    }
}