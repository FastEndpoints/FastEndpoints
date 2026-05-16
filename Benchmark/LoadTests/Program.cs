using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using NBomber.Contracts;
using NBomber.CSharp;

const int routeId = 123;

const string payload = """
                       {"FirstName":"xxx","LastName":"yyy","Age":23,"PhoneNumbers":["1111111111","2222222222","3333333333","4444444444","5555555555"]}
                       """;

var options = LoadTestOptions.Parse(args);

if (options is null)
{
    PrintUsage();

    return 1;
}

foreach (var targetName in options.TargetName is "all"
                               ? ["fastendpoints", "minimalapi", "mvc"]
                               : new[] { options.TargetName })
{
    using var target = CreateTarget(targetName);

    if (target is null)
    {
        PrintUsage();

        return 1;
    }

    RunThroughputTest(target, options);
}

return 0;

static ILoadTestTarget? CreateTarget(string targetName)
    => targetName switch
    {
        "fastendpoints" or "fe" => LoadTestTarget.Create<FEBench.Program>("fastendpoints"),
        "minimalapi" or "minimal" => LoadTestTarget.Create<MinimalApi.Program>("minimalapi"),
        "mvc" or "controller" or "controllers" => LoadTestTarget.Create<MvcControllers.Program>("mvc"),
        _ => null
    };

static void RunThroughputTest(ILoadTestTarget target, LoadTestOptions options)
{
    var scenario = Scenario.Create(target.Name, _ => SendRequest(target.Client))
                           .WithWarmUpDuration(options.WarmUpDuration)
                           .WithLoadSimulations(
                               Simulation.KeepConstant(
                                   copies: options.ConcurrentUsers,
                                   during: options.TestDuration));

    NBomberRunner.RegisterScenarios(scenario)
                 .WithTestSuite("FastEndpoints Benchmark")
                 .WithTestName($"{target.Name}-throughput-{options.ConcurrentUsers}users")
                 .Run();
}

static async Task<IResponse> SendRequest(HttpClient client)
{
    using var request = new HttpRequestMessage(HttpMethod.Post, $"benchmark/ok/{routeId}");
    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

    return response.IsSuccessStatusCode
               ? Response.Ok(statusCode: response.StatusCode.ToString())
               : Response.Fail(statusCode: response.StatusCode.ToString());
}

static void PrintUsage()
{
    Console.WriteLine($"Usage: dotnet run -c Release --project Benchmark/LoadTests -- <fastendpoints|minimalapi|mvc|all> [--users {LoadTestOptions.DefaultConcurrentUsers}] [--duration {LoadTestOptions.DefaultDurationSeconds}] [--warmup {LoadTestOptions.DefaultWarmUpSeconds}]");
}

sealed record LoadTestOptions(string TargetName, int ConcurrentUsers, TimeSpan WarmUpDuration, TimeSpan TestDuration)
{
    public const int DefaultConcurrentUsers = 8;
    public const int DefaultWarmUpSeconds = 5;
    public const int DefaultDurationSeconds = 60;

    public static LoadTestOptions? Parse(string[] args)
    {
        if (args.Length == 0)
            return null;

        var targetName = args[0].Trim().ToLowerInvariant();
        var concurrentUsers = DefaultConcurrentUsers;
        var warmUpSeconds = DefaultWarmUpSeconds;
        var durationSeconds = DefaultDurationSeconds;

        for (var i = 1; i < args.Length; i++)
        {
            var option = args[i].Trim().ToLowerInvariant();

            switch (option)
            {
                case "--users" or "-u":
                    if (!TryReadPositiveInt(args, ref i, out concurrentUsers))
                        return null;
                    break;

                case "--duration" or "-d":
                    if (!TryReadPositiveInt(args, ref i, out durationSeconds))
                        return null;
                    break;

                case "--warmup" or "-w":
                    if (!TryReadPositiveInt(args, ref i, out warmUpSeconds))
                        return null;
                    break;

                default:
                    return null;
            }
        }

        return new(targetName,
                   concurrentUsers,
                   TimeSpan.FromSeconds(warmUpSeconds),
                   TimeSpan.FromSeconds(durationSeconds));
    }

    static bool TryReadPositiveInt(string[] args, ref int index, out int value)
    {
        value = 0;

        return ++index < args.Length &&
               int.TryParse(args[index], out value) &&
               value > 0;
    }
}

interface ILoadTestTarget : IDisposable
{
    string Name { get; }
    HttpClient Client { get; }
}

static class LoadTestTarget
{
    public static ILoadTestTarget Create<TProgram>(string name) where TProgram : class
        => new LoadTestTarget<TProgram>(name);
}

sealed class LoadTestTarget<TProgram> : ILoadTestTarget where TProgram : class
{
    readonly WebApplicationFactory<TProgram> _factory = new();

    public LoadTestTarget(string name)
    {
        Name = name;
        Client = _factory.CreateClient();
    }

    public string Name { get; }
    public HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }
}
