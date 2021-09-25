# FastEndpoints

An easy to use Web-Api framework (which encourages CQRS and Vertical Slice Architecture) built as an extension to the Asp.Net pipeline. Performance is on par with `.net 6 minimal apis` and is 2X faster; uses only half the memory; and outperforms a traditional MVC controller by about **[73k requests per second](#bombardier-load-test)** on a Ryzen 3700X desktop.

## Try it out...
install from nuget: `Install-Package FastEndpoints` **(currently beta)**

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
public class MyRequest : IRequest
{
    [From(Claim.UserName)]
    public string UserName { get; set; }  //this value will be auto populated from the user claim

    public int Id { get; set; }
    public string? Name { get; set; }
    public int Price { get; set; }
}
```

### Response DTO
```csharp
public class Response : IResponse
{
    public string? Name { get; internal set; }
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
            Allow.Inventory_Update_Item); //declarative permission based authentication
    }

    protected override async Task ExecuteAsync(MyRequest req, CancellationToken ct)
    {
        //can do further validation here in addition to FluentValidations rules
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

### FastEndpoints *(72,920 more requests per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    144989.43   13594.10  199851.96
  Latency        3.41ms   378.95us    65.00ms
  HTTP codes:
    1xx - 0, 2xx - 1462226, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    73.34MB/s
```
### AspNet Minimal Api
```
Statistics        Avg      Stdev        Max
  Reqs/sec    144416.77   14313.21  171576.65
  Latency        3.43ms     1.37ms   347.00ms
  HTTP codes:
    1xx - 0, 2xx - 1456040, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    73.02MB/s
```
### AspNet MapControllers
```
Statistics        Avg      Stdev        Max
  Reqs/sec     74056.92   19197.47  372446.94
  Latency        6.71ms     1.89ms   416.00ms
  HTTP codes:
    1xx - 0, 2xx - 745069, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    37.37MB/s
```
### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec     72069.51   14094.86   96234.73
  Latency        6.83ms   712.49us    89.01ms
  HTTP codes:
    1xx - 0, 2xx - 731659, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    36.56MB/s
```

**parameters used:** `-c 500 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/`
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
| FastEndpointsEndpoint |  78.47 μs | 1.522 μs | 1.753 μs |  1.00 |    0.00 | 2.4414 |     21 KB |
|    MinimalApiEndpoint |  77.05 μs | 1.519 μs | 2.496 μs |  0.97 |    0.04 | 2.4414 |     21 KB |
|  AspNetMapControllers | 148.36 μs | 2.922 μs | 5.270 μs |  1.88 |    0.07 | 5.3711 |     44 KB |
|         AspNetCoreMVC | 150.66 μs | 2.984 μs | 6.550 μs |  1.90 |    0.09 | 5.3711 |     45 KB |
