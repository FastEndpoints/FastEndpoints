## Head-To-Head Benchmark

|       Method             |      Mean |    Error |   StdDev | Ratio | RatioSD |  Gen 0 |  Gen 1 | Allocated |
|------------------------- |----------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|
|               MinimalApi |  67.61 μs | 0.437 μs | 0.229 μs |  1.00 |    0.00 | 2.4000 |      - |     20 KB |
|            FastEndpoints |  68.58 μs | 0.833 μs | 0.436 μs |  1.01 |    0.01 | 2.3000 |      - |     19 KB |
| FastEndpoints Throttling |  73.57 μs | 0.891 μs | 0.530 μs |  1.09 |    0.01 | 2.5000 | 0.2000 |     21 KB |
|            AspNetCoreMVC | 103.39 μs | 2.265 μs | 1.348 μs |  1.53 |    0.02 | 3.1000 | 0.1000 |     26 KB |
|                   Carter | 583.91 μs | 2.318 μs | 1.533 μs |  8.64 |    0.03 | 5.7000 | 2.8000 |     47 KB |

## Bombardier Load Test

### FastEndpoints *(45,526 more requests per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    151312.88   15386.74  222185.18
  Latency        3.34ms   219.93us    87.00ms
  HTTP codes:
    1xx - 0, 2xx - 4591511, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    74.56MB/s
```
### FastEndpoints (With throttling)
```
Statistics        Avg      Stdev        Max
  Reqs/sec    124985.03   20969.32  181578.21
  Latency        4.17ms     3.68ms      0.90s
  HTTP codes:
    1xx - 0, 2xx - 3669532, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    64.27MB/s
```
### AspNet Minimal Api
```
Statistics        Avg      Stdev        Max
  Reqs/sec    148957.22   16322.82  207723.19
  Latency        3.39ms     0.88ms   359.61ms
  HTTP codes:
    1xx - 0, 2xx - 4526855, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    75.69MB/s
```
### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec    105786.47   12016.03  225220.72
  Latency        4.80ms     1.88ms   565.00ms
  HTTP codes:
    1xx - 0, 2xx - 3194131, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    53.41MB/s
```
### Carter Module
```
Statistics        Avg      Stdev        Max
  Reqs/sec      5547.30    2723.39   17380.62
  Latency       92.11ms    12.87ms   580.22ms
  HTTP codes:
    1xx - 0, 2xx - 166993, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:     2.78MB/s
```

**parameters used:** `-c 512 -m POST -f "body.json" -H "Content-Type:application/json"  -d 30s`

**hardware used:** `AMD Ryzen 7 3700X (8c/16t), 16GB RAM, Windows 11`

<!-- .\bomb.exe -c 512 -m POST -f "body.json" -H "Content-Type:application/json"  -d 30s http://localhost:5000/benchmark/ok/123 -->
<!-- .\bomb.exe -c 512 -m POST -f "body.json" -H "Content-Type:application/json" -H "X-Forwarded-For:000.000.000.000"  -d 30s http://localhost:5000/benchmark/throttle/123 -->

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

