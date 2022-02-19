#pragma warning disable CA1822
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text;
using System.Text.Json;

namespace Runner;

[
    SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 10, invocationCount: 10000),
    MemoryDiagnoser
]
public class Benchmarks
{
    private static HttpClient FastEndpointClient { get; } = new WebApplicationFactory<FastEndpointsBench.Program>().CreateClient();
    private static HttpClient FEThrottleClient { get; } = new WebApplicationFactory<FastEndpointsBench.Program>().CreateClient();
    private static HttpClient MinimalClient { get; } = new WebApplicationFactory<MinimalApi.Program>().CreateClient();
    private static HttpClient MvcClient { get; } = new WebApplicationFactory<MvcControllers.Program>().CreateClient();
    private static readonly StringContent Payload = new(
        JsonSerializer.Serialize(new
        {
            FirstName = "xxx",
            LastName = "yyy",
            Age = 23,
            PhoneNumbers = new[] {
                "1111111111",
                "2222222222",
                "3333333333",
                "4444444444",
                "5555555555"
            }
        }), Encoding.UTF8, "application/json");

    [Benchmark(Baseline = true)]
    public Task FastEndpoints()
    {
        var msg = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{FastEndpointClient.BaseAddress}benchmark/ok/123"),
            Content = Payload
        };

        return FastEndpointClient.SendAsync(msg);
    }

    [Benchmark]
    public Task MinimalApi()
    {
        var msg = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{MinimalClient.BaseAddress}benchmark/ok/123"),
            Content = Payload
        };

        return MinimalClient.SendAsync(msg);
    }

    [Benchmark]
    public Task FastEndpointsThrottling()
    {
        var msg = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{FEThrottleClient.BaseAddress}benchmark/throttle/123"),
            Content = Payload
        };
        msg.Headers.Add("X-Forwarded-For", $"000.000.000.{Random.Shared.NextInt64(100, 200)}");

        return FEThrottleClient.SendAsync(msg);
    }

    [Benchmark]
    public Task AspNetCoreMVC()
    {
        var msg = new HttpRequestMessage()
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri($"{MvcClient.BaseAddress}benchmark/ok/123"),
            Content = Payload
        };

        return MvcClient.SendAsync(msg);
    }
}