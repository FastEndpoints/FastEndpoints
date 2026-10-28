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

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task PreRouting_Check_Preserves_Endpoint_Gated_Authentication(bool cookie, bool revoked)
    {
        var token = CreateToken("user");
        var ctx = NewContext(anonymous: null, queryToken: token, endpointGated: true, cookie: cookie);
        var routedChecker = new Checker(revoked ? [token] : []);
        var earlyChecker = new Checker(
            async c =>
            {
                c.SetEndpoint(new(null, EndpointMetadataCollection.Empty, "test"));
                await routedChecker.Invoke(c);

                if (NextCalled(c))
                {
                    var result = await c.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                    result.Succeeded.ShouldBeTrue();
                }
            });

        await earlyChecker.Invoke(ctx);

        earlyChecker.Checked.ShouldBeEmpty();
        routedChecker.Checked.ShouldBe([token]);
        ctx.Items["validations"].ShouldBe(1);
        NextCalled(ctx).ShouldBe(!revoked);
        ctx.Response.StatusCode.ShouldBe(revoked ? 401 : 200);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreRouting_Check_Leaves_Authentication_For_Downstream_Middleware(bool cookie)
    {
        var token = CreateToken("user");
        var checker = new Checker();
        var ctx = NewContext(anonymous: null, queryToken: token, endpointGated: true, cookie: cookie);

        await checker.Invoke(ctx);

        checker.Checked.ShouldBeEmpty();
        ctx.SetEndpoint(new(null, EndpointMetadataCollection.Empty, "test"));
        var result = await ctx.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);

        result.Succeeded.ShouldBeTrue();
        ctx.Items["validations"].ShouldBe(1);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cookie_Only_Endpoint_Does_Not_Invoke_Unrelated_Jwt(bool explicitScheme)
    {
        var checker = new Checker();
        var ctx = NewContext(
            anonymous: false,
            addJwtBearer: false,
            configure: services =>
                       {
                           services.AddAuthentication("Cookies")
                                   .AddCookie("Cookies")
                                   .AddJwtBearer("UnusedJwt", o => o.Events = new()
                                   {
                                       OnMessageReceived = _ => throw new InvalidOperationException("Unrelated JWT invoked")
                                   });
                           services.AddAuthorization();
                       });
        ctx.SetEndpoint(new(null, new EndpointMetadataCollection(new AuthorizeAttribute
        {
            AuthenticationSchemes = explicitScheme ? "Cookies" : ""
        }), "cookies"));

        await checker.Invoke(ctx);

        NextCalled(ctx).ShouldBeTrue();
        checker.Checked.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("explicit", false, false)]
    [InlineData("explicit", true, true)]
    [InlineData("named", false, true)]
    [InlineData("named", true, false)]
    [InlineData("direct", false, false)]
    [InlineData("direct", true, true)]
    [InlineData("default", false, true)]
    [InlineData("default", true, false)]
    [InlineData("default-policy", false, false)]
    [InlineData("default-policy", true, true)]
    [InlineData("fallback", false, false)]
    [InlineData("fallback", true, true)]
    [InlineData("authenticate-default", false, false)]
    [InlineData("authenticate-default", true, true)]
    public async Task Only_Selected_Jwt_Scheme_Is_Checked(string selection, bool cookie, bool revoked)
    {
        var token = CreateToken("selected");
        var checker = new Checker(revoked ? [token] : []);
        var ctx = NewSchemeContext(selection, cookie, multiple: false);
        SetSchemeToken(ctx, "SelectedJwt", token, cookie);

        await checker.Invoke(ctx);

        checker.Checked.ShouldBe([token]);
        NextCalled(ctx).ShouldBe(!revoked);
        ctx.Response.StatusCode.ShouldBe(revoked ? 401 : 200);

        var result = await ctx.AuthenticateAsync("SelectedJwt");
        result.Succeeded.ShouldBeTrue();
        ctx.Items["SelectedJwt"].ShouldBe(1);
    }

    [Theory]
    [InlineData("explicit", false, false)]
    [InlineData("explicit", true, true)]
    [InlineData("named", false, true)]
    [InlineData("named", true, false)]
    [InlineData("direct", false, false)]
    [InlineData("direct", true, true)]
    public async Task All_Selected_Jwt_Schemes_Are_Checked(string selection, bool cookie, bool revoked)
    {
        var first = CreateToken("first");
        var second = CreateToken("second");
        var checker = new Checker(revoked ? [second] : []);
        var ctx = NewSchemeContext(selection, cookie, multiple: true);
        SetSchemeToken(ctx, "SelectedJwt", first, cookie);
        SetSchemeToken(ctx, "SecondJwt", second, cookie);

        await checker.Invoke(ctx);

        checker.Checked.ShouldBe([first, second]);
        NextCalled(ctx).ShouldBe(!revoked);
        ctx.Response.StatusCode.ShouldBe(revoked ? 401 : 200);
        ctx.Items["SelectedJwt"].ShouldBe(1);
        ctx.Items["SecondJwt"].ShouldBe(1);
    }

    static DefaultHttpContext NewSchemeContext(string selection, bool cookie, bool multiple)
    {
        string[] selected = multiple ? ["SelectedJwt", "SecondJwt"] : ["SelectedJwt"];
        var policy = new AuthorizationPolicyBuilder(selected).RequireAuthenticatedUser().Build();
        var ctx = NewContext(
            anonymous: false,
            addJwtBearer: false,
            configure: services =>
                       {
                           var auth = services.AddAuthentication(selection == "default" ? "SelectedJwt" : "Cookies")
                                              .AddCookie("Cookies")
                                              .AddJwtBearer("UnusedJwt", o => o.Events = new()
                                              {
                                                  OnMessageReceived = _ => throw new InvalidOperationException("Unrelated JWT invoked")
                                              });

                           if (selection == "authenticate-default")
                               services.Configure<AuthenticationOptions>(o => o.DefaultAuthenticateScheme = "SelectedJwt");

                           foreach (var scheme in selected)
                           {
                               auth.AddJwtBearer(scheme, o =>
                               {
                                   o.SaveToken = true;
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
                                                               c.HttpContext.Items[scheme] = (int)(c.HttpContext.Items[scheme] ?? 0) + 1;
                                                               c.Token = cookie ? c.Request.Cookies[scheme] : c.Request.Query[scheme].FirstOrDefault();

                                                               return Task.CompletedTask;
                                                           }
                                   };
                               });
                           }

                           services.AddAuthorization(o =>
                           {
                               o.AddPolicy("selected", policy);

                               if (selection == "default-policy")
                                   o.DefaultPolicy = policy;
                               else if (selection == "fallback")
                                   o.FallbackPolicy = policy;
                           });
                       });
        var metadata = selection switch
        {
            "explicit" => new EndpointMetadataCollection(new AuthorizeAttribute { AuthenticationSchemes = string.Join(",", selected) }),
            "named" => new EndpointMetadataCollection(new AuthorizeAttribute("selected")),
            "direct" => new EndpointMetadataCollection(policy),
            "default" or "default-policy" or "authenticate-default" => new EndpointMetadataCollection(new AuthorizeAttribute()),
            _ => EndpointMetadataCollection.Empty
        };
        ctx.SetEndpoint(new(null, metadata, "selected"));

        return ctx;
    }

    static void SetSchemeToken(HttpContext ctx, string scheme, string token, bool cookie)
    {
        if (cookie)
            ctx.Request.Headers.Cookie = string.IsNullOrEmpty(ctx.Request.Headers.Cookie) ? $"{scheme}={token}" : $"{ctx.Request.Headers.Cookie}; {scheme}={token}";
        else
            ctx.Request.QueryString = ctx.Request.QueryString.Add(scheme, token);
    }

    static DefaultHttpContext NewContext(bool? anonymous, string? header = null, string? queryToken = null, bool addJwtBearer = true, bool endpointGated = false, bool cookie = false, Action<IServiceCollection>? configure = null)
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
                                                        if (endpointGated && c.HttpContext.GetEndpoint() is null)
                                                            return Task.CompletedTask;

                                                        var token = cookie ? c.Request.Cookies["access_token"] : c.Request.Query["access_token"].FirstOrDefault();

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

        configure?.Invoke(services);

        var ctx = new DefaultHttpContext { RequestServices = services.BuildServiceProvider().CreateScope().ServiceProvider };

        if (header is not null)
            ctx.Request.Headers.Authorization = header;

        if (queryToken is not null)
        {
            if (cookie)
                ctx.Request.Headers.Cookie = $"access_token={queryToken}";
            else
                ctx.Request.QueryString = QueryString.Create("access_token", queryToken);
        }

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

    sealed class Checker(RequestDelegate next, params string[] revoked) : JwtRevocationMiddleware(next)
    {
        public Checker(params string[] revoked) : this(
            ctx =>
            {
                ctx.Items["next"] = true;

                return Task.CompletedTask;
            },
            revoked) { }

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
