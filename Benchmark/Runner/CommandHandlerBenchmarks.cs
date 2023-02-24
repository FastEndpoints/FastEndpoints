#pragma warning disable CA1822
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Runner;

[MemoryDiagnoser, SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 10, invocationCount: 10000)]
public class CommandHandlerBenchmarks
{
    private static HttpClient NoCommandHandlerClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();
    private static HttpClient WithCommandHandlerClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();

    [Benchmark(Baseline = true)]
    public Task NoCommandHandler()
    {
        return NoCommandHandlerClient.SendAsync(
            new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{NoCommandHandlerClient.BaseAddress}command-handler-1"),
            });
    }

    [Benchmark]
    public Task WithCommandHandler()
    {
        return WithCommandHandlerClient.SendAsync(
            new HttpRequestMessage()
            {
                Method = HttpMethod.Get,
                RequestUri = new Uri($"{WithCommandHandlerClient.BaseAddress}command-handler-2"),
            });
    }
}
