## Head-To-Head Benchmark

|                Method |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|---------------------- |----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|
|    AspNet Minimal Api |  84.06 μs | 3.721 μs | 2.214 μs |  1.00 |    0.00 | 2.6000 |      - |     21 KB |
|         FastEndpoints |  84.93 μs | 2.539 μs | 1.328 μs |  1.01 |    0.04 | 2.5000 |      - |     21 KB |
| AspNet MVC Controller | 112.75 μs | 5.053 μs | 3.007 μs |  1.34 |    0.06 | 3.4000 | 0.1000 |     28 KB |
|         Carter Module | 602.90 μs | 7.663 μs | 5.069 μs |  7.17 |    0.22 | 5.9000 | 2.9000 |     49 KB |

## Bombardier Load Test

### FastEndpoints *(46,341 more requests per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    141291.31   16967.65  230746.18
  Latency        3.50ms   479.73us    95.00ms
  HTTP codes:
    1xx - 0, 2xx - 1428565, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    71.64MB/s
```
### AspNet Minimal Api
```
Statistics        Avg      Stdev        Max
  Reqs/sec    145044.77   17963.08  250949.81
  Latency        3.39ms   284.73us    62.00ms
  HTTP codes:
    1xx - 0, 2xx - 1471745, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    73.78MB/s
```
### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec     94950.85   11212.91  118144.21
  Latency        5.22ms     2.46ms   489.99ms
  HTTP codes:
    1xx - 0, 2xx - 957021, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    47.82MB/s
```
### Carter Module
```
Statistics        Avg      Stdev        Max
  Reqs/sec      7655.85    3286.81   25178.10
  Latency       65.09ms    11.37ms   453.00ms
  HTTP codes:
    1xx - 0, 2xx - 77050, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:     3.85MB/s
```

**parameters used:** `-c 500 -m POST -f "body.json" -H "Content-Type:application/json"  -d 10s`

**hardware used:** `AMD Ryzen 7 3700X (8c/16t), 16GB RAM, Windows 11`

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

