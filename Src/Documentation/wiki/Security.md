# securing endpoints
endpoints are secure by default and you'd have to call `AllowAnnonymous()` in the constructor if you'd like to allow unauthenticated users to access a particular endpoint.

## jwt bearer authentication
support for easy jwt bearer authentication is provided. you simply need to install the `FastEndpoints.Security` package and register it in the middleware pipeline like so:

### Program.cs
```csharp
using FastEndpoints;
using FastEndpoints.Security;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer("TokenSigningKey");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```

### generating jwt tokens
you can generate a jwt token for sending to the client with an endpoint that signs in users like so:

```csharp
public class UserLoginEndpoint : Endpoint<LoginRequest>
{
    public UserLoginEndpoint()
    {
        Verbs(Http.POST);
        Routes("/api/login");
        AllowAnnonymous();
    }

    protected override async Task HandleAsync(LoginRequest req, CancellationToken ct)
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
    public ManageUsers()
    {
        Verbs(Http.PUT);
        Routes("/api/users/update");
        Policies("ManagersOnlyPolicy");
    }       
}
```
### declarative security policies
instead of registering each security policy at startup you can selectively specify security requirements for each endpoint in the endpoint constructors themselves like so:
```csharp
public class RestrictedEndpoint : Endpoint<RestrictedRequest>
{
    public RestrictedEndpoint()
    {
        Verbs(Http.POST);
        Routes("/api/restricted");
        Claims("AdminID", "UserType");
        Roles("Admin", "Manager");
        Permissions("UpdateUsersPermission", "DeleteUsersPermission");
    }
}
```
**Claims() method**

with this method you are specifying that if a user has all the specified claims, access should be allowed. you can specify to allow access if they have any one of the given claims by using the overload:
```csharp
Claims(allowAny: true, "SomeClaimType", "AnotherClaimType");
```

**Permissions() method**

just like above, you can specify that all permissions mentioned must be present to allow access or using the following overload, you can allow access even if any one of the permissions is present:
```csharp
Permissions(allowAny: true, "SomePermission", "AnotherPermission");
```

**Roles() method**

roles behaves differently than the above as in access will always be allowed if a user has any of the specified roles assigned to them.

## other authentication providers
all authorization providers compatible with the `asp.net` middleware pipeline can be registered and used like above. the only difference is that you use the methods mentioned above to restrict access to endpoints rather than using the `[Authorize]` attribute as you would typically do.