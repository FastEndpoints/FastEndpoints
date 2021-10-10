#pragma warning disable CA1822
using BenchmarkDotNet.Attributes;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Runner
{
    [
        SimpleJob(launchCount: 1, warmupCount: 1, targetCount: 10, invocationCount: 10000),
        MemoryDiagnoser
    ]
    public class Benchmarks
    {
        private static HttpClient FastEndpointClient { get; } = new WebApplicationFactory<FastEndpointsBench.Program>().CreateClient();
        private static HttpClient MinimalClient { get; } = new WebApplicationFactory<MinimalApi.Program>().CreateClient();
        private static HttpClient MvcClient { get; } = new WebApplicationFactory<MvcControllers.Program>().CreateClient();
        private static HttpClient CarterClient { get; } = new WebApplicationFactory<CarterModules.Program>().CreateClient();

        [Benchmark(Baseline = true)]
        public async Task FastEndpointsEndpoint()
        {
            await FastEndpointClient.POSTAsync<FastEndpointsBench.Request, FastEndpointsBench.Response>(
                "/benchmark/ok/123", new()
                {
                    FirstName = "xxc",
                    LastName = "yyy",
                    Age = 23,
                    PhoneNumbers = new[] {
                        "1111111111",
                        "2222222222",
                        "3333333333",
                        "4444444444",
                        "5555555555"
                    }
                });
        }

        [Benchmark]
        public async Task MinimalApiEndpoint()
        {
            await MinimalClient.POSTAsync<MinimalApi.Request, MinimalApi.Response>(

                "/benchmark/ok/123", new()
                {
                    FirstName = "xxc",
                    LastName = "yyy",
                    Age = 23,
                    PhoneNumbers = new[] {
                        "1111111111",
                        "2222222222",
                        "3333333333",
                        "4444444444",
                        "5555555555"
                    }
                });
        }

        [Benchmark]
        public async Task CarterModule()
        {
            await CarterClient.POSTAsync<CarterModules.Request, CarterModules.Response>(

                 "/benchmark/ok/123", new()
                 {
                     FirstName = "xxc",
                     LastName = "yyy",
                     Age = 23,
                     PhoneNumbers = new[] {
                        "1111111111",
                        "2222222222",
                        "3333333333",
                        "4444444444",
                        "5555555555"
                     }
                 });
        }

        [Benchmark]
        public async Task AspNetCoreMVC()
        {
            await MvcClient.POSTAsync<MvcControllers.Request, MvcControllers.Response>(

                "/Home/Index/123", new()
                {
                    FirstName = "xxc",
                    LastName = "yyy",
                    Age = 23,
                    PhoneNumbers = new[] {
                        "1111111111",
                        "2222222222",
                        "3333333333",
                        "4444444444",
                        "5555555555"
                    }
                });
        }
    }
}
