using System.Text;
using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace JwtBearerValidation;

[CollectionDefinition(Name, DisableParallelization = true)]
public class JwtBearerValidationCollection
{
    public const string Name = nameof(JwtBearerValidationCollection);
}

[Collection(JwtBearerValidationCollection.Name)]
public class JwtBearerValidationTests : IDisposable
{
    const string SigningKey = "jwt-bearer-validation-tests-signing-key-0123456789";
    const string Audience = "my-api";
    const string Issuer = "my-issuer";

    readonly IServiceResolver? _previousResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;

    public JwtBearerValidationTests()
    {
        var provider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(provider, provider.GetRequiredService<IHttpContextAccessor>(), isUnitTestMode: true);
    }

    public void Dispose()
        => ServiceResolver.Instance = _previousResolver!;

    [Theory]
    [InlineData("singular", true)]
    [InlineData("audience", true)]
    [InlineData("plural", true)]
    [InlineData("plural-explicit", true)]
    [InlineData("none", false)]
    [InlineData("explicit-without-values", false)]
    public async Task Audience_And_Issuer_Validation_Follows_Configured_Values(string config, bool validates)
    {
        var tvp = ResolveValidationParameters(
            o =>
            {
                var p = o.TokenValidationParameters;

                switch (config)
                {
                    case "singular":
                        p.ValidAudience = Audience;
                        p.ValidIssuer = Issuer;

                        break;
                    case "audience":
                        o.Audience = Audience;
                        p.ValidIssuers = [Issuer];

                        break;
                    case "plural":
                        p.ValidAudiences = [Audience];
                        p.ValidIssuers = [Issuer];

                        break;
                    case "plural-explicit":
                        p.ValidAudiences = [Audience];
                        p.ValidIssuers = [Issuer];
                        p.ValidateAudience = true;
                        p.ValidateIssuer = true;

                        break;
                    case "explicit-without-values":
                        p.ValidateAudience = true;
                        p.ValidateIssuer = true;

                        break;
                }
            });

        tvp.ValidateAudience.ShouldBe(validates);
        tvp.ValidateIssuer.ShouldBe(validates);

        (await IsValid(tvp, Audience, Issuer)).ShouldBeTrue();
        (await IsValid(tvp, "other-api", Issuer)).ShouldBe(!validates);
        (await IsValid(tvp, Audience, "evil-issuer")).ShouldBe(!validates);
    }

    static TokenValidationParameters ResolveValidationParameters(Action<JwtBearerOptions> bearerOptions)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        //signing key is left unset so the process-wide key used by JwtSigningOptions.KeyResolver isn't touched
        services.AddAuthenticationJwtBearer(_ => { }, bearerOptions);

        using var provider = services.BuildServiceProvider();
        var tvp = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                          .Get(JwtBearerDefaults.AuthenticationScheme)
                          .TokenValidationParameters
                          .Clone();
        tvp.IssuerSigningKeyResolver = null;
        tvp.IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));

        return tvp;
    }

    static async Task<bool> IsValid(TokenValidationParameters tvp, string audience, string issuer)
    {
        var token = new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Audience = audience,
                Issuer = issuer,
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)
            });

        return (await new JsonWebTokenHandler().ValidateTokenAsync(token, tvp)).IsValid;
    }
}
