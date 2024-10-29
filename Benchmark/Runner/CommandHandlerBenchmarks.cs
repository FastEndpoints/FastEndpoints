using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;

#pragma warning disable CA1822

namespace Runner;

[MemoryDiagnoser, SimpleJob(launchCount: 1, warmupCount: 1, iterationCount: 10, invocationCount: 10000)]
public class CommandHandlerBenchmarks
{
    static HttpClient NoCommandHandlerClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();
    static HttpClient WithCommandHandlerClient { get; } = new WebApplicationFactory<FEBench.Program>().CreateClient();

    [Benchmark(Baseline = true)]
    public Task NoCommandHandler()
        => NoCommandHandlerClient.SendAsync(
            new()
            {
                Method = HttpMethod.Get,
                RequestUri = new($"{NoCommandHandlerClient.BaseAddress}command-handler-1")
            });

    [Benchmark]
    public Task WithCommandHandler()
        => WithCommandHandlerClient.SendAsync(
            new()
            {
                Method = HttpMethod.Get,
                RequestUri = new($"{WithCommandHandlerClient.BaseAddress}command-handler-2")
            });
}