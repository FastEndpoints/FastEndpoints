# ApiExpress

An easy to use Web API framework (which encourages CQRS and Vertical Slice Architecture) built as an extension to the Asp.Net pipeline. It is a great alternative to the new minimal APIs that require manual endpoint mapping.

Current State: **NOT PRODUCTION READY!!!**

## Try it out...
there is still no nuget package published. so you'd have to clone this git repo and reference the `/Src/ApiExpress.cs` as a project reference from a .net core 6 project. or you can play around with the sample project in `/Web/Web.csproj`.

# Code Sample:

### Program.cs
```csharp
using ApiExpress;

var builder = WebApplication.CreateBuilder();
builder.Services.AddApiExpress();
builder.Services.AddAuthenticationJWTBearer("Key");

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseApiExpress();
app.Run();
```

### Request DTO
```csharp
public class MyRequest : IRequest
{
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
        Routes("/api/test/{id}");
        Verbs(Http.POST, Http.PATCH);

        Roles("Admin", "Manager");
        Policies("TopManagement", "Auditors");
        Permissions(
            Allow.Inventory_Create_Item,
            Allow.Inventory_Retrieve_Item,
            Allow.Inventory_Update_Item);
    }

    protected override Task ExecuteAsync(MyRequest req, CancellationToken cancellation)
    {
        if (req.Price < 100)
            AddError(r => r.Price, "Price is too low!");

        AddError("This is a general error!");

        ThrowIfAnyErrors(); //this will send a 400 error response with a json object containing error details.

        Logger.LogInformation("this is your first endpoint!");

        var res = new MyResponse
        {
            Message = $"the route parameter value is: {req.Id}",
            Name = req.Name,
            Price = req.Price
        };

        return SendAsync(res);
    }
}
```

that's it. all of your `Endpoint` definitions are automatically discovered on app startup and routes automatically mapped.

# Stay tuned...

if the above api looks interesting to you, watch this repo for updates. you are welcome to submit PRs or suggest features/ report bugs using github issues.

# Benchmark results

 <!-- .\bomb.exe -c 100 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/benchmark/ok/123 -->

## Bombardier load test

### ApiExpress Endpoint
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
  Reqs/sec     73447.69    2983.87   82207.93
  Latency        1.36ms    81.53us    22.00ms
  HTTP codes:
    1xx - 0, 2xx - 734693, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    36.86MB/s
```

### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec     72418.24    2895.27   79805.72
  Latency        1.38ms   103.11us    29.00ms
  HTTP codes:
    1xx - 0, 2xx - 724404, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    36.19MB/s
```

**parameters used:** 
`-c 100 -m POST -f "body.json" -H "Content-Type : application/json"  -d 10s http://localhost:5000/`
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

|               Method |     Mean |   Error |  StdDev | Ratio | RatioSD |  Gen 0 | Allocated |
|--------------------- |---------:|--------:|--------:|------:|--------:|-------:|----------:|
|   ApiExpress Endpoint | 106.0 μs | 2.12 μs | 4.73 μs |  1.00 |    0.00 | 3.6621 |     30 KB |
| AspNet MapControllers | 146.5 μs | 2.84 μs | 2.79 μs |  1.41 |    0.06 | 5.3711 |     44 KB |
| AspNet MVC Controller | 148.2 μs | 2.92 μs | 3.36 μs |  1.42 |    0.06 | 5.3711 |     45 KB |