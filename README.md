# FastEndpoints

An easy to use Web API framework (which encourages CQRS and Vertical Slice Architecture) built as an extension to the Asp.Net pipeline. 
**FastEndpoints** is 2X faster; uses only half the memory; and outperforms a traditional MVC controller by about 39,0000 requests per second on a typical desktop computer. 
It is a great alternative to the new minimal APIs that require manual endpoint mapping.

## Try it out...
install from nuget: `Install-Package FastEndpoints` **(currently beta)**

**note:** the minimum required sdk version is `.net 6.0` (preview at the moment)

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

 <!-- .\bomb.exe -c 100 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/benchmark/ok/123 -->

## Bombardier load test

### FastEndpoints *(39,500 requests more per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    110569.18    4482.43  124218.28
  Latency        0.90ms    50.54us    16.00ms
  HTTP codes:
    1xx - 0, 2xx - 1105885, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    55.47MB/s
```

### AspNet MapControllers
```
Statistics        Avg      Stdev        Max
  Reqs/sec     71621.34    3551.21  100210.44
  Latency        1.39ms   186.22us    42.00ms
  HTTP codes:
    1xx - 0, 2xx - 716949, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    35.95MB/s
```

### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec     70981.56    2983.29   78947.63
  Latency        1.40ms   118.66us    27.00ms
  HTTP codes:
    1xx - 0, 2xx - 710040, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    35.48MB/s
```

**parameters used:** 
`-c 100 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/Home/Index/123`
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
| FastEndpointsEndpoint |  75.30 μs | 1.252 μs | 1.171 μs |  1.00 |    0.00 | 2.4414 |      - |     21 KB |
|  AspNetMapControllers | 144.44 μs | 2.778 μs | 3.307 μs |  1.92 |    0.04 | 5.3711 | 0.2441 |     44 KB |
|   AspNetMVCController | 148.82 μs | 2.110 μs | 1.974 μs |  1.98 |    0.04 | 5.3711 |      - |     45 KB |
