# FastEndpoints

An alternative for building RESTful Web APIs with ASP.Net 6 which encourages CQRS and Vertical Slice Architecture.

`FastEndpoints` offers a more elegant solution than the `Minimal APIs` and `MVC Controllers`.

Performance is on par with the `Minimal APIs` and is faster; uses less memory; and outperforms a traditional `MVC Controller` by about **[34k requests per second](#bombardier-load-test)** on a Ryzen 3700X desktop.

## Features

- Define your endpoints in multiple class files (even in deeply nested folders)
- Auto discovery and registration of endpoints
- Attribute-free endpoint definitions (no attribute argument type restrictions)
- Secure by default and supports most authentication/authorization providers
- Built-in support for JWT Bearer auth scheme
- Supports policy/permission/role/claim based security
- Declarative security policy building (inside each endpoint)
- Supports any IOC container (compatible with asp.net)
- Dependencies are automatically property injected
- Model binding support from route/json body/claims/forms
- Model validation using FluentValidation rules
- Convenient business logic validation and error responses
- Easy access to environment and configuration settings
- Supports pipeline behaviors like MediatR
- Supports in-process pub/sub event notifications
- Auto discovery of event notification handlers
- Convenient integration testing (route-less and strongly-typed)
- Supports unit testing endpoint logic without http layer
- Plays well with the asp.net middleware pipeline
- Supports swagger/serilog/etc.
- Visual studio extension (vsix) for easy vertical slice feature scaffolding
- Plus anything else the `minimal apis` can do...

## Try it out...
install from nuget: `Install-Package FastEndpoints`

**note:** the minimum required sdk version is `.net 6.0`

# Code Sample:

### Program.cs
```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer("SecretKey");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```

### Request.cs
```csharp
public class MyRequest
{
    [From(Claim.UserName)]
    public string UserName { get; set; }  //this value will be auto populated from the user claim

    public int Id { get; set; }
    public string Name { get; set; }
    public int Price { get; set; }
}
```

### Validator.cs
```csharp
public class MyValidator : Validator<MyRequest>
{
    public MyValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("Id is required!");
        RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required!");
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price is required!");
    }
}
```

### Response.cs
```csharp
public class MyResponse
{
    public string Name { get; internal set; }
    public int Price { get; set; }
    public string? Message { get; set; }
}
```

### Endpoint.cs
```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public ILogger<MyEndpoint>? Logger { get; set; } //dependency injected

    public MyEndpoint()
    {
        Routes("/api/test/{id}");
        Verbs(Http.POST, Http.PATCH);
        Roles("Admin", "Manager");
        Policies("ManagementTeamCanAccess", "AuditorsCanAccess");
        Permissions(
            Allow.Inventory_Create_Item,
            Allow.Inventory_Retrieve_Item,
            Allow.Inventory_Update_Item);
        Claims(Claim.CustomerID);
    }

    protected override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        //can do further validation here in addition to FluentValidation rules
        if (req.Price < 100)
            AddError(r => r.Price, "Price is too low!");

        AddError("This is a general error!");

        ThrowIfAnyErrors(); //breaks the flow and sends a 400 error response containing error details.

        var isProduction = Env.IsProduction(); //read environment
        var smtpServer = Config["SMTP:HostName"]; //read configuration

        var res = new MyResponse //typed response makes integration testing easy
        {
            Message = $"the route parameter value is: {req.Id}",
            Name = req.Name,
            Price = req.Price
        };

        await SendAsync(res);
    }
}
```

all of your `Endpoint` definitions are automatically discovered on app startup. no manual mapping is required like with `minimal apis`.

# Documentation
documentation will be available within a few weeks once **v1.0** is released. in the meantime have a browse through the `Web`, `Test` and `Benchmark` projects to see more examples.

# Benchmark results

 <!-- .\bomb.exe -c 500 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/benchmark/ok/123 -->

## Bombardier load test

### FastEndpoints *(33,772 more requests per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    134251.40   16085.58  190809.19
  Latency        3.68ms     1.35ms   371.64ms
  HTTP codes:
    1xx - 0, 2xx - 1357086, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    68.05MB/s
```
### AspNet Minimal Api
```
Statistics        Avg      Stdev        Max
  Reqs/sec    136898.40   13732.59  185851.32
  Latency        3.62ms   470.46us    94.99ms
  HTTP codes:
    1xx - 0, 2xx - 1379343, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    69.19MB/s
```
### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec    100479.98   13649.02  123388.00
  Latency        4.90ms     1.67ms   375.00ms
  HTTP codes:
    1xx - 0, 2xx - 1019171, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    50.91MB/s
```
### Carter Module
```
Statistics        Avg      Stdev        Max
  Reqs/sec      7592.05    3153.39   18037.17
  Latency       65.45ms    17.77ms   560.62ms
  HTTP codes:
    1xx - 0, 2xx - 76638, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:     3.82MB/s
```

**parameters used:** `-c 500 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s`
<!-- ```
{
  "FirstName": "xxc",
  "LastName": "yyy",
  "Age": 23,
  "PhoneNumbers": [
    "1111111111",
    "2222222222",
    "3333333333",
    "4444444444",
    "5555555555"
  ]
}
``` -->

## BenchmarkDotNet head-to-head results

|                Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|---------------------- |----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|
| FastEndpointsEndpoint |  83.03 μs | 5.007 μs | 3.312 μs |  1.00 |    0.00 | 2.6000 | 0.1000 |     22 KB |
|    MinimalApiEndpoint |  83.51 μs | 3.781 μs | 2.501 μs |  1.01 |    0.03 | 2.5000 |      - |     21 KB |
|         AspNetCoreMVC | 114.20 μs | 3.806 μs | 2.518 μs |  1.38 |    0.06 | 3.4000 | 0.2000 |     28 KB |
|          CarterModule | 607.48 μs | 1.455 μs | 0.962 μs |  7.33 |    0.29 | 5.9000 | 2.9000 |     48 KB |