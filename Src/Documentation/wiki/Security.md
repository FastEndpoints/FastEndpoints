# securing endpoints
endpoints are secure by default and you'd have to call `AllowAnonymous()` in the configuration if you'd like to allow unauthenticated users to access a particular endpoint.

## jwt bearer authentication
support for easy jwt bearer authentication is provided. you simply need to install the `FastEndpoints.Security` package and register it in the middleware pipeline like so:

### program.cs
```csharp
global using FastEndpoints;
global using FastEndpoints.Security; //add this

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer("TokenSigningKey"); //add this

var app = builder.Build();
app.UseAuthentication(); //add this
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```

### generating jwt tokens
you can generate a jwt token for sending to the client with an endpoint that signs in users like so:

```csharp
public class UserLoginEndpoint : Endpoint<LoginRequest>
{
    public override void Configure()
    {
        Post("/api/login");
        AllowAnonymous();
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        if (req.Username == "admin" && req.Password == "pass")
        {
            var jwtToken = JWTBearer.CreateToken(
                signingKey: "TokenSigningKey",
                expireAt: DateTime.UtcNow.AddDays(1),
                claims: new[] { ("Username", req.Username), ("UserID", "001") },
                roles: new[] { "Admin", "Management" },
                permissions: new[] { "ManageInventory", "ManageUsers" });

            await SendAsync(new
            {
                Username = req.Username,
                Token = jwtToken
            });
        }
        else
        {
            ThrowError("The supplied credentials are invalid!");
        }
    }
}
```

## endpoint authorization

once an authentication provider is registered such as jwt bearer as shown above, you can restrict access to users based on the following:

- policies
- claims
- roles
- permissions

### pre-built security policies
security policies can be pre-built and registered during app startup and endpoints can choose to allow access to users based on the registered policy names like so:

**startup**
```csharp
builder.Services.AddAuthorization(o =>
    o.AddPolicy("ManagersOnlyPolicy", b =>
        b.RequireRole("Manager")
         .RequireClaim("ManagerID")));
```
**endpoint**
```csharp
public class UpdateUserEndpoint : Endpoint<UpdateUserRequest>
{
    public override void Configure()
    {
        Verbs(Http.PUT);
        Routes("/api/users/update");
        Policies("ManagersOnlyPolicy");
    }       
}
```
### declarative security policies
instead of registering each security policy at startup you can selectively specify security requirements for each endpoint in the endpoint configuration itself like so:
```csharp
public class RestrictedEndpoint : Endpoint<RestrictedRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/api/restricted");
        Claims("AdminID", "EmployeeID");
        Roles("Admin", "Manager");
        Permissions("UpdateUsersPermission", "DeleteUsersPermission");
    }
}
```
**Claims() method**

with this method you are specifying that if a user principal has `ANY` of the specified claims, access should be allowed. 
if the requirement is to allow access only if `ALL` specified claims are present, you can use the `ClaimsAll()` method.

**Permissions() method**

just like above, you can specify that `ANY` of the specified permissions should allow access. Or require `ALL` of the specified permissions by using the `PermissionsAll()` method.

**Roles() method**

similarly, you are specifying that `ANY` of the given roles should allow access to a user principal who has it.

**AllowAnonymous() method**

use this method if you'd like to allow unauthenticated users to access a particular endpoint. it is also possible to specify which http verbs you'd like to allow anonymous access to like so:
```csharp
public class RestrictedEndpoint : Endpoint<RestrictedRequest>
{
    public override void Configure()
    {
        Verbs(Http.POST, Http.PUT, Http.PATCH);
        Routes("/api/restricted");
        AllowAnonymous(Http.POST);
    }
}
```
the above endpoint is listening for all 3 http methods on the same route but only `POST` method is allowed to be accessed anonymously. it is useful for example when you'd like to use the same handler logic for create/replace/update scenarios and create operation is allowed to be done by anonymous users.

using just `AllowAnonymous()` without any arguments means all verbs are allowed anonymous access.

## other auth providers
all auth providers compatible with the `asp.net` middleware pipeline can be registered and used like above.

> [!TIP]
> here's an **[example project](https://github.com/dj-nitehawk/FastEndpoints-Auth0-Demo)** using **[Auth0](https://auth0.com/access-management)** with permissions.

## multiple authentication schemes
it is possible to register multiple auth schemes at startup and specify per endpoint which schemes are to be used for authenticating incoming requests.

**startup**
```csharp
builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(o => o.SlidingExpiration = true) // cookie auth
.AddJwtBearer(o =>                          // jwt bearer auth
{
    o.Authority = $"https://{builder.Configuration["Auth0:Domain"]}/";
    o.Audience = builder.Configuration["Auth0:Audience"];
});
```

**endpoint**
```csharp
public override void Configure()
{
    Get("/account/profile");
    AuthSchems(JwtBearerDefaults.AuthenticationScheme);
}
```
in the above example, we're registering both cookie and jwt bearer auth schemes and in the endpoint we're saying only jwt bearer auth scheme should be used for authenticating incoming requests to the endpoint. you can specify multiple schemes and if an incoming request isn't using `any` of the said schemes, access will not be allowed.