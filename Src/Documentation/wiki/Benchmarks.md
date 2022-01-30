## Head-To-Head Benchmark

|        Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|-------------- |----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|
| FastEndpoints |  77.18 μs | 4.878 μs | 2.903 μs |  0.98 |    0.03 | 2.5000 | 0.1000 |     21 KB |
|    MinimalApi |  78.86 μs | 4.863 μs | 2.894 μs |  1.00 |    0.00 | 2.6000 | 0.1000 |     21 KB |
| AspNetCoreMVC | 113.74 μs | 4.761 μs | 2.833 μs |  1.44 |    0.05 | 3.4000 | 0.1000 |     28 KB |
|  CarterModule | 606.86 μs | 2.482 μs | 1.477 μs |  7.70 |    0.28 | 5.9000 | 2.9000 |     48 KB |

## Bombardier Load Test

### FastEndpoints *(47% more requests per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    139710.52   14282.62  205535.97
  Latency        3.62ms   249.06us    62.00ms
  HTTP codes:
    1xx - 0, 2xx - 4239464, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    68.86MB/s
```
### AspNet Minimal Api
```
Statistics        Avg      Stdev        Max
  Reqs/sec    139055.59   14499.22  256384.63
  Latency        3.64ms   317.69us    69.61ms
  HTTP codes:
    1xx - 0, 2xx - 4217185, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    70.50MB/s
```
### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec     95202.53   13207.26  136648.45
  Latency        5.32ms     1.88ms   569.00ms
  HTTP codes:
    1xx - 0, 2xx - 2882790, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    48.02MB/s
```
### Carter Module
```
Statistics        Avg      Stdev        Max
  Reqs/sec      5619.10    2911.79   34528.87
  Latency       91.01ms     9.52ms   481.00ms
  HTTP codes:
    1xx - 0, 2xx - 168989, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:     2.82MB/s
```

**parameters used:** `-c 512 -m POST -f "body.json" -H "Content-Type:application/json"  -d 30s`

**hardware used:** `AMD Ryzen 7 3700X (8c/16t), 16GB RAM, Windows 11`

<!-- .\bomb.exe -c 512 -m POST -f "body.json" -H "Content-Type:application/json"  -d 30s http://localhost:5000/benchmark/ok/123 -->
<!-- .\bomb.exe -c 512 -m POST -f "body.json" -H "Content-Type:application/json"  -d 30s http://localhost:5000/Home/Index/123 -->
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

