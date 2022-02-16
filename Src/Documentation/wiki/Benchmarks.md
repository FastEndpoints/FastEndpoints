## Head-To-Head Benchmark

|                        Method |      Mean | Ratio |  Gen 0 |  Gen 1 | Allocated |
|------------------------------ |----------:|------:|-------:|-------:|----------:|
|                 FastEndpoints |  66.11 μs |  1.00 | 2.3000 |      - |     19 KB |
|                   Minimal Api |  67.87 μs |  1.03 | 2.4000 |      - |     20 KB |
| FastEndpoints With Throttling |  70.78 μs |  1.07 | 2.5000 |      - |     20 KB |
|     AspNetCore MVC Controller | 104.26 μs |  1.57 | 3.1000 | 0.1000 |     26 KB |
|                 Carter Module | 591.73 μs |  8.95 | 5.7000 | 2.8000 |     47 KB |

## Bombardier Load Test

```
hardware: AMD Ryzen 7 3700X (8c/16t), 16GB RAM, Windows 11
parameters: -c 512 -m POST -f "body.json" -H "Content-Type:application/json" -d 30s
```

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
### FastEndpoints with throttling
```
Statistics        Avg      Stdev        Max
  Reqs/sec    134116.29   16805.24  171542.89
  Latency        3.84ms     1.58ms   368.00ms
  HTTP codes:
    1xx - 0, 2xx - 3992219, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    69.93MB/s
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

## TechEmpower Benchmark (Preliminary)
<a target="_blank" href="https://www.techempower.com/benchmarks/#section=test&runid=fa199ea7-b3db-4cd5-bab8-85ff67217db0&hw=ph&test=json&l=zik0zh-sf&c=8&a=2">
  <img src="https://dev-to-uploads.s3.amazonaws.com/uploads/articles/ksvhrqxeipucnsuakitw.png">
</a>

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

