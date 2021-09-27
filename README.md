# FastEndpoints

An easy to use Web-Api framework (which encourages CQRS and Vertical Slice Architecture) built as an extension to the Asp.Net pipeline. Performance is on par with `.net 6 minimal apis` and is ~1.5X faster; uses less memory; and outperforms a traditional MVC controller by about **[39k requests per second](#bombardier-load-test)** on a Ryzen 3700X desktop.

## Try it out...
install from nuget: `Install-Package FastEndpoints` **(currently release candidate)**

**note:** the minimum required sdk version is `.net 6.0` (preview atm)

# Code Sample:

### Program.cs
```csharp
using FastEndpoints;

var builder = WebApplication.CreateBuilder();
builder.Services.AddFastEndpoints();
builder.Services.AddAuthenticationJWTBearer("SecretKey");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseFastEndpoints();
app.Run();
```

### Request DTO
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

### Request Validator
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

### Response DTO
```csharp
public class MyResponse
{
    public string Name { get; internal set; }
    public int Price { get; set; }
    public string? Message { get; set; }
}
```

### Endpoint Definition
```csharp
public class MyEndpoint : Endpoint<MyRequest>
{
    public ILogger<MyEndpoint>? Logger { get; set; } //automatically injected from services

    public MyEndpoint()
    {
        //no longer hindered by attribute limitations
        Routes("/api/test/{id}");
        Verbs(Http.POST, Http.PATCH);
        Roles("Admin", "Manager");
        Policies("ManagementTeamCanAccess", "AuditorsCanAccess");
        Permissions(
            Allow.Inventory_Create_Item,
            Allow.Inventory_Retrieve_Item,
            Allow.Inventory_Update_Item); //declarative permission based authorization
    }

    protected override async Task HandleAsync(MyRequest req, CancellationToken ct)
    {
        //can do further validation here in addition to FluentValidation rules
        if (req.Price < 100)
            AddError(r => r.Price, "Price is too low!");

        AddError("This is a general error!");

        ThrowIfAnyErrors(); //breaks the flow and sends a 400 error response containing error details.

        Logger.LogInformation("this is your first endpoint!"); //dependency injected logger

        var isProduction = Env.IsProduction(); //read environment
        var smtpServer = Config["SMTP:HostName"]; //read configuration

        var res = new MyResponse //typed response to make integration tests convenient
        {
            Message = $"the route parameter value is: {req.Id}",
            Name = req.Name,
            Price = req.Price
        };

        await SendAsync(res);
    }
}
```

that's mostly it. all of your `Endpoint` definitions are automatically discovered on app startup and routes automatically mapped.

# Documentation
proper documentation will be available within a few weeks once **v1.0** is released. in the meantime have a browse through the `Web`, `Test` and `Benchmark` projects to see more examples.

# Benchmark results

 <!-- .\bomb.exe -c 500 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/benchmark/ok/123 -->

## Bombardier load test

### FastEndpoints *(39,377 more requests per second than mvc controller)*
```
Statistics       Avg        Stdev     Max
  Reqs/sec    140494.96   13112.46  174985.42
  Latency        3.51ms     1.10ms   361.00ms
  HTTP codes:
    1xx - 0, 2xx - 1417846, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    71.12MB/s
```
### AspNet Minimal Api
```
Statistics       Avg        Stdev     Max
  Reqs/sec    140644.35   14557.75  171137.84
  Latency        3.51ms     2.43ms   398.00ms
  HTTP codes:
    1xx - 0, 2xx - 1419449, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    71.19MB/s
```
### AspNet MapControllers
```
Statistics       Avg       Stdev      Max
  Reqs/sec    104587.47   11267.99  129709.65
  Latency        4.74ms     2.09ms   416.00ms
  HTTP codes:
    1xx - 0, 2xx - 1054018, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    52.86MB/s
```
### AspNet MVC Controller
```
Statistics       Avg       Stdev      Max
  Reqs/sec    101117.36   12152.01  135669.68
  Latency        4.90ms     2.47ms   385.00ms
  HTTP codes:
    1xx - 0, 2xx - 1018455, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    50.88MB/s
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

|                Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 | Allocated |
|---------------------- |----------:|---------:|---------:|------:|--------:|-------:|----------:|
|    MinimalApiEndpoint |  72.54 μs | 0.156 μs | 0.121 μs |  0.97 |    0.01 | 2.4414 |     21 KB |
| **FastEndpointsEndpoint** |  74.64 μs | 0.493 μs | 0.461 μs |  1.00 |    0.00 | 2.4414 |     21 KB |
|  AspNetMapControllers | 110.96 μs | 2.209 μs | 5.377 μs |  1.46 |    0.05 | 3.1738 |     28 KB |
|         AspNetCoreMVC | 115.44 μs | 2.282 μs | 3.686 μs |  1.53 |    0.06 | 3.4180 |     28 KB |