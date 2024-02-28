using Web.Services;
using _Claim = System.Security.Claims.Claim;

namespace Admin.Login;

public class Endpoint : Endpoint<Request, Response>
{
    readonly IConfiguration _config;

    public Endpoint(ILogger<Endpoint> logger, IEmailService emailService, IConfiguration configuration)
    {
        _config = configuration;
        logger.LogInformation("constructor injection works!");
        _ = emailService.SendEmail();
    }

    public override void Configure()
    {
        Verbs(Http.POST, Http.PUT, Http.PATCH);
        Routes("admin/login");
        AllowAnonymous();
        Options(b => b.RequireCors(p => p.AllowAnyOrigin()));
        RequestBinder(new RequestBinder<Request>(BindingSource.JsonBody | BindingSource.QueryParams));
        Description(
            b => b.Accepts<Request>("application/json")
                  .Produces<Response>(200, "application/json")
                  .Produces(400)
                  .Produces(403),
            clearDefaults: true);
        Summary(
            s =>
            {
                s.Summary = "this is a short summary";
                s.Description = "this is the long description of the endpoint";
                s.RequestParam(r => r.UserName, "overriden username text");
                s.ExampleRequest = new()
                {
                    UserName = "custom example user name from summary",
                    Password = "custom example password from summary"
                };
                s[200] = "all good";
                s[400] = "indicates an error";
                s[403] = "forbidden when login fails";
                s[201] = "new resource created";
                s.ResponseHeaders.Add(new(200, "x-some-custom-header"));
            });
        SerializerContext(AdminLogin.Default);
        Version(0, 1);
    }

    public override Task HandleAsync(Request r, CancellationToken ct)
    {
        if (r.UserName == "admin" && r.Password == "pass")
        {
            var expiryDate = DateTime.UtcNow.AddDays(1);

            var userPermissions = Allow.Admin;

            var userClaims = new _Claim[]
            {
                new(Claim.UserName, r.UserName),
                new(Claim.UserType, Role.Admin),
                new(Claim.AdminID, "USR0001"),
                new("test-claim", "test claim val")
            };

            var userRoles = new[]
            {
                Role.Admin,
                Role.Staff
            };

            var token = JwtBearer.CreateToken(
                o =>
                {
                    o.SigningKey = _config["TokenKey"]!;
                    o.ExpireAt = expiryDate;
                    o.User.Permissions.AddRange(userPermissions);
                    o.User.Roles.AddRange(userRoles);
                    o.User.Claims.AddRange(userClaims);
                });

            return SendAsync(
                new()
                {
                    JWTToken = token,
                    ExpiryDate = expiryDate,
                    Permissions = Allow.NamesFor(userPermissions)
                });
        }
        AddError("Authentication Failed!");

        return SendErrorsAsync();
    }
}

public class Endpoint_V1 : Endpoint
{
    public Endpoint_V1(ILogger<Endpoint_V1> logger, IEmailService emailService, IConfiguration configuration) : base(
        logger,
        emailService,
        configuration) { }

    public override void Configure()
    {
        base.Configure();
        Throttle(5, 5);
        Version(1, deprecateAt: 2);
    }
}

public class Endpoint_V2 : Endpoint<EmptyRequest, object>
{
    public override void Configure()
    {
        Get("admin/login");
        Version(2);
        AllowAnonymous();
    }

    public override Task HandleAsync(EmptyRequest r, CancellationToken ct)
        => SendAsync(2);
}