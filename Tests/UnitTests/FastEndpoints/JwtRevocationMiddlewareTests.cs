using System.Security.Claims;
using System.Text;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Security;

public class JwtRevocationMiddlewareTests
{
    const string SigningKey = "a signing key that is long enough for hmac sha256 tokens";

    [Fact]
    public async Task Header_Token_Is_Checked_When_No_Endpoint_Is_Resolved()
    {
        var checker = new Checker("revoked token");
        var ctx = NewContext(anonymous: null, header: "Bearer revoked token", addJwtBearer: false);

        await checker.Invoke(ctx);

        ctx.Response.StatusCode.ShouldBe(401);
        NextCalled(ctx).ShouldBeFalse();
    }

    [Fact]
    public async Task Allow_Anonymous_Endpoint_Is_Not_Checked()
    {
        var checker = new Checker("revoked token");
        var ctx = NewContext(anonymous: true, header: "Bearer revoked token");

        await checker.Invoke(ctx);

        NextCalled(ctx).ShouldBeTrue();
        checker.Checked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Unvalidated_Header_Token_Is_Still_Checked()
    {
        var checker = new Checker("revoked token");
        var ctx = NewContext(anonymous: false, header: "Bearer revoked token");

        await checker.Invoke(ctx);

        ctx.Response.StatusCode.ShouldBe(401);
        NextCalled(ctx).ShouldBeFalse();
    }

    [Fact]
    public async Task Revoked_Token_From_Another_Source_Is_Rejected()
    {
        var token = CreateToken("victim");
        var checker = new Checker(token);
        var ctx = NewContext(anonymous: false, queryToken: token);

        await checker.Invoke(ctx);

        ctx.Response.StatusCode.ShouldBe(401);
        NextCalled(ctx).ShouldBeFalse();
    }

    [Fact]
    public async Task Revoked_Token_From_Another_Source_Is_Rejected_When_Header_Token_Is_Valid()
    {
        var revoked = CreateToken("victim");
        var checker = new Checker(revoked);
        var ctx = NewContext(anonymous: false, header: $"Bearer {CreateToken("attacker")}", queryToken: revoked);

        await checker.Invoke(ctx);

        ctx.Response.StatusCode.ShouldBe(401);
        NextCalled(ctx).ShouldBeFalse();
    }

    [Fact]
    public async Task Revoked_Token_Is_Rejected_When_No_Endpoint_Is_Resolved()
    {
        var token = CreateToken("victim");
        var checker = new Checker(token);
        var ctx = NewContext(anonymous: null, queryToken: token);

        await checker.Invoke(ctx);

        ctx.Response.StatusCode.ShouldBe(401);
        NextCalled(ctx).ShouldBeFalse();
    }

    [Fact]
    public async Task Valid_Token_From_Another_Source_Is_Checked_And_Passes()
    {
        var token = CreateToken("user");
        var checker = new Checker();
        var ctx = NewContext(anonymous: false, queryToken: token);

        await checker.Invoke(ctx);

        NextCalled(ctx).ShouldBeTrue();
        checker.Checked.ShouldBe([token]);
    }

    [Fact]
    public async Task Valid_Header_Token_Is_Checked_Once()
    {
        var token = CreateToken("user");
        var checker = new Checker();
        var ctx = NewContext(anonymous: false, header: $"Bearer {token}");

        await checker.Invoke(ctx);

        NextCalled(ctx).ShouldBeTrue();
        checker.Checked.ShouldBe([token]);
    }

    [Fact]
    public async Task Token_Is_Validated_Once_Per_Request()
    {
        var token = CreateToken("user");
        var checker = new Checker();
        var ctx = NewContext(anonymous: false, queryToken: token);

        await checker.Invoke(ctx);
        var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

        result.Succeeded.ShouldBeTrue();
        ctx.Items["validations"].ShouldBe(1);
    }

    static DefaultHttpContext NewContext(bool? anonymous, string? header = null, string? queryToken = null, bool addJwtBearer = true)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        if (addJwtBearer)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(
                        o =>
                        {
                            o.SaveToken = true;
                            o.MapInboundClaims = false;
                            o.TokenValidationParameters = new()
                            {
                                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                                ValidateIssuer = false,
                                ValidateAudience = false
                            };
                            o.Events = new()
                            {
                                OnMessageReceived = c =>
                                                    {
                                                        var token = c.Request.Query["access_token"].FirstOrDefault();

                                                        if (!string.IsNullOrEmpty(token))
                                                            c.Token = token;

                                                        return Task.CompletedTask;
                                                    },
                                OnTokenValidated = c =>
                                                   {
                                                       c.HttpContext.Items["validations"] = (int)(c.HttpContext.Items["validations"] ?? 0) + 1;

                                                       return Task.CompletedTask;
                                                   }
                            };
                        });
        }

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider().CreateScope().ServiceProvider };

        if (header is not null)
            ctx.Request.Headers.Authorization = header;

        if (queryToken is not null)
            ctx.Request.QueryString = QueryString.Create("access_token", queryToken);

        if (anonymous is not null)
        {
            var metadata = anonymous.Value ? new EndpointMetadataCollection(new AllowAnonymousAttribute()) : EndpointMetadataCollection.Empty;
            ctx.SetEndpoint(new(null, metadata, "test"));
        }

        return ctx;
    }

    static string CreateToken(string subject)
        => new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Subject = new([new Claim("sub", subject)]),
                Expires = DateTime.UtcNow.AddMinutes(5),
                SigningCredentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)
            });

    static bool NextCalled(HttpContext ctx)
        => ctx.Items.ContainsKey("next");

    sealed class Checker(params string[] revoked) : JwtRevocationMiddleware(
        ctx =>
        {
            ctx.Items["next"] = true;

            return Task.CompletedTask;
        })
    {
        public List<string> Checked { get; } = [];

        protected override Task<bool> JwtTokenIsValidAsync(string jwtToken, CancellationToken ct)
        {
            Checked.Add(jwtToken);

            return Task.FromResult(!revoked.Contains(jwtToken));
        }

        protected override Task SendTokenRevokedResponseAsync(HttpContext ctx, CancellationToken ct)
        {
            ctx.Response.StatusCode = 401;

            return Task.CompletedTask;
        }
    }
}
