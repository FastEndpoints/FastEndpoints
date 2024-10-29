using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;

#pragma warning disable CA1822

namespace Runner;

[MemoryDiagnoser, SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 10, invocationCount: 10000)]
public class EmptyRequestBenchmarks
{
    static HttpClient EmptyClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();
    static HttpClient ObjectClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();

    [Benchmark(Baseline = true)]
    public Task EmptyRequest()
        => EmptyClient.SendAsync(
            new()
            {
                Method = HttpMethod.Get,
                RequestUri = new($"{EmptyClient.BaseAddress}empty-request")
            });

    [Benchmark]
    public Task ObjectRequest()
        => ObjectClient.SendAsync(
            new()
            {
                Method = HttpMethod.Get,
                RequestUri = new($"{ObjectClient.BaseAddress}object-request")
            });
}