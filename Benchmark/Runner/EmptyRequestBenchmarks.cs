#pragma warning disable CA1822
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Runner;

[MemoryDiagnoser, SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 10, invocationCount: 10000)]
public class EmptyRequestBenchmarks
{
    private static HttpClient emptyClient { get; } = new WebApplicationFactory<FastEndpointsBench.Program>().CreateClient();
    private static HttpClient objectClient { get; } = new WebApplicationFactory<FastEndpointsBench.Program>().CreateClient();

    [Benchmark(Baseline = true)]
    public Task EmptyRequest()
    {
        return emptyClient.SendAsync(
            new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{emptyClient.BaseAddress}empty-request"),
            });
    }

    [Benchmark]
    public Task ObjectRequest()
    {
        return objectClient.SendAsync(
            new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{objectClient.BaseAddress}object-request"),
            });
    }
}
