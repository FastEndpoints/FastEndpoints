## Head-To-Head Benchmark

|                  Method |     Mean | Ratio |  Gen 0 |  Gen 1 | Allocated |
|------------------------ |---------:|------:|-------:|-------:|----------:|
|           FastEndpoints | 46.80 μs |  1.00 | 2.1000 |      - |     17 KB |
|              MinimalApi | 48.06 μs |  1.03 | 2.1000 | 0.1000 |     18 KB |
| FastEndpointsThrottling | 54.74 μs |  1.17 | 2.2000 |      - |     18 KB |
|           AspNetCoreMVC | 78.82 μs |  1.68 | 2.9000 | 0.1000 |     24 KB |

## Bombardier Load Test *(best out of 5 runs)*

```
hardware: AMD Ryzen 7 3700X (8c/16t), 16GB RAM, Windows 11
parameters: -c 512 -m POST -f "body.json" -H "Content-Type:application/json" -d 30s
```

### FastEndpoints *(45,000 more requests per second than mvc controller)*
```
Statistics        Avg      Stdev        Max
  Reqs/sec    152719.41   15319.65  237177.27
  Latency        3.31ms   233.06us    61.00ms
  HTTP codes:
    1xx - 0, 2xx - 4635227, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    75.30MB/s
```
### AspNet Minimal Api
```
Statistics        Avg      Stdev        Max
  Reqs/sec    149415.35   14544.34  185050.95
  Latency        3.38ms     0.89ms   431.99ms
  HTTP codes:
    1xx - 0, 2xx - 4529011, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    75.73MB/s
```
### FastEndpoints with throttling
```
Statistics        Avg      Stdev        Max
  Reqs/sec    137547.83   18167.83  215500.00
  Latency        3.69ms     2.02ms   568.63ms
  HTTP codes:
    1xx - 0, 2xx - 4154347, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    72.77MB/s
```
### AspNet MVC Controller
```
Statistics        Avg      Stdev        Max
  Reqs/sec    107381.33   13064.54  184073.63
  Latency        4.73ms     1.25ms   416.00ms
  HTTP codes:
    1xx - 0, 2xx - 3245222, 3xx - 0, 4xx - 0, 5xx - 0
    others - 0
  Throughput:    54.26MB/s
```

## TechEmpower Benchmark (Preliminary)
<a target="_blank" href="https://www.techempower.com/benchmarks/#section=test&runid=79350aca-3138-4f06-9f2a-d9ca470028f6&hw=ph&test=json&l=zik0zh-sf&c=8&a=2">
  <img src="https://dev-to-uploads.s3.amazonaws.com/uploads/articles/ei5kn1xm09b1unokcy54.png">
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

