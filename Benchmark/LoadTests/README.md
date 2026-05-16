# NBomber Load Tests

Run each target individually from the repo root:

```sh
dotnet run -c Release --project Benchmark/LoadTests -- fastendpoints
dotnet run -c Release --project Benchmark/LoadTests -- minimalapi
dotnet run -c Release --project Benchmark/LoadTests -- mvc
```

Or run all targets sequentially:

```sh
dotnet run -c Release --project Benchmark/LoadTests -- all
```

Optional settings:

```sh
dotnet run -c Release --project Benchmark/LoadTests -- fastendpoints --users 128 --duration 120 --warmup 10
```

All three tests use the same request:

- `POST /benchmark/ok/123`
- same JSON payload as `Benchmark/Runner/Benchmarks.cs`
- 5 second warm-up by default
- throughput run for 1 minute with 64 concurrent users by default

NBomber is not rate-limiting these tests. It keeps 64 request loops active and reports completed requests/sec, so compare the `OK/sec` throughput together with latency percentiles and failures.
