#pragma warning disable CA1822
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Runner;

[MemoryDiagnoser, SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 10, invocationCount: 10000)]
public class EmptyRequestBenchmarks
{
    private static HttpClient EmptyClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();
    private static HttpClient ObjectClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();

    [Benchmark(Baseline = true)]
    public Task EmptyRequest()
    {
        return EmptyClient.SendAsync(
            new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{EmptyClient.BaseAddress}empty-request"),
            });
    }

    [Benchmark]
    public Task ObjectRequest()
    {
        return ObjectClient.SendAsync(
            new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{ObjectClient.BaseAddress}object-request"),
            });
    }
}