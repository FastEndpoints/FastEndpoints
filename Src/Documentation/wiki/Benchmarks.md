## Bombardier Load Test

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

 <!-- .\bomb.exe -c 500 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s http://localhost:5000/benchmark/ok/123 -->

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

## Head-To-Head Benchmark

|                Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|---------------------- |----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|
| FastEndpointsEndpoint |  83.03 μs | 5.007 μs | 3.312 μs |  1.00 |    0.00 | 2.6000 | 0.1000 |     22 KB |
|    MinimalApiEndpoint |  83.51 μs | 3.781 μs | 2.501 μs |  1.01 |    0.03 | 2.5000 |      - |     21 KB |
|         AspNetCoreMVC | 114.20 μs | 3.806 μs | 2.518 μs |  1.38 |    0.06 | 3.4000 | 0.2000 |     28 KB |
|          CarterModule | 607.48 μs | 1.455 μs | 0.962 μs |  7.33 |    0.29 | 5.9000 | 2.9000 |     48 KB |