using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Misc;

public class SerializerConfigurationTests
{
    const int RequestCount = 8;

    static ManualResetEventSlim _releaseResponses = new(false);
    static int _configureCalls;
    static int _requestsEntered;
    static int _responsesSent;

    [Fact]
    public async Task Serializer_Configuration_Is_Process_Wide_And_Race_Free_When_Hosts_Start_In_Parallel()
    {
        var previousSerializerConfigured = SerializerConfigured;
        var previousSerializerOptions = Config.SerOpts.Options;
        var previousServiceResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;
        var previousRoutePrefix = Config.EpOpts.RoutePrefix;
        var previousEndpointFilter = Config.EpOpts.Filter;
        var previousEndpointConfigurator = Config.EpOpts.Configurator;

        WebApplication? appA = null;
        WebApplication? appB = null;

        ResetTestState();
        Config.SerOpts.Options = new();
        Config.EpOpts.RoutePrefix = null;
        Config.EpOpts.Filter = null;
        Config.EpOpts.Configurator = null;
        SerializerConfigured = false;

        try
        {
            appA = BuildApp();
            appA.UseFastEndpoints(ConfigureSerializer);
            await appA.StartAsync(TestContext.Current.CancellationToken);

            // ReSharper disable once ShortLivedHttpClient
            using var client = new HttpClient();
            client.BaseAddress = new($"{appA.Urls.First()}/");

            var responseTasks = Enumerable.Range(0, RequestCount)
                                          .Select(_ => client.GetAsync("serializer-race-test", TestContext.Current.CancellationToken))
                                          .ToArray();

            SpinWait.SpinUntil(
                        () => Volatile.Read(ref _requestsEntered) == RequestCount,
                        TimeSpan.FromSeconds(10))
                    .ShouldBeTrue("all requests should enter the first host before the second host starts");

            appB = BuildApp();
            appB.UseFastEndpoints(ConfigureSerializer);
            await appB.StartAsync(TestContext.Current.CancellationToken);

            // If the second host reused the process-wide serializer configuration, its config action never ran.
            // Release the first host responses here. Before the one-time startup gate, the second config action
            // released these responses while it was still mutating the freshly-published JsonSerializerOptions,
            // which let the first host freeze that instance and made the second mutation throw.
            _releaseResponses.Set();

            var responses = await Task.WhenAll(responseTasks);

            foreach (var response in responses)
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);

                var body = await response.Content.ReadFromJsonAsync<SerializerRaceResponse>(TestContext.Current.CancellationToken);
                body?.Message.ShouldBe("ok");
            }

            Volatile.Read(ref _configureCalls).ShouldBe(1, "FastEndpoints serializer/global config is process-wide and should run once per process");
        }
        finally
        {
            _releaseResponses.Set();

            if (appB is not null)
                await appB.DisposeAsync();

            if (appA is not null)
                await appA.DisposeAsync();

            Config.SerOpts.Options = previousSerializerOptions;
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousEndpointFilter;
            Config.EpOpts.Configurator = previousEndpointConfigurator;
            SerializerConfigured = previousSerializerConfigured;
            SetServiceResolver(previousServiceResolver);
            ResetTestState();
        }
    }

    static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes.Add(typeof(SerializerRaceEndpoint)));

        return builder.Build();
    }

    static void ConfigureSerializer(Config config)
    {
        var call = Interlocked.Increment(ref _configureCalls);

        if (call == 2)
        {
            SpinWait.SpinUntil(
                        () => Volatile.Read(ref _requestsEntered) == RequestCount,
                        TimeSpan.FromSeconds(10))
                    .ShouldBeTrue("all first-host requests should be waiting before releasing responses from the second config action");

            _releaseResponses.Set();

            SpinWait.SpinUntil(
                        () => Volatile.Read(ref _responsesSent) == RequestCount,
                        TimeSpan.FromSeconds(10))
                    .ShouldBeTrue("first-host responses should serialize with the second host's published serializer options");
        }

        config.Serializer.Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }

    static bool SerializerConfigured
    {
        get => (bool)SerializerConfiguredField.GetValue(null)!;
        set => SerializerConfiguredField.SetValue(null, value);
    }

    static FieldInfo SerializerConfiguredField { get; } =
        typeof(MainExtensions).GetField("_serializerConfigured", BindingFlags.NonPublic | BindingFlags.Static)!;

    static FieldInfo ServiceResolverInstanceField { get; } =
        typeof(ServiceResolver).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;

    static void SetServiceResolver(IServiceResolver? resolver)
        => ServiceResolverInstanceField.SetValue(null, resolver);

    static void ResetTestState()
    {
        _releaseResponses.Dispose();
        _releaseResponses = new(false);
        _configureCalls = 0;
        _requestsEntered = 0;
        _responsesSent = 0;
    }

    sealed class SerializerRaceEndpoint : EndpointWithoutRequest<SerializerRaceResponse>
    {
        public override void Configure()
        {
            Get("serializer-race-test");
            AllowAnonymous();
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref _requestsEntered);
            _releaseResponses.Wait(ct);

            await Send.OkAsync(new() { Message = "ok" }, ct);
            Interlocked.Increment(ref _responsesSent);
        }
    }

    sealed class SerializerRaceResponse
    {
        public string Message { get; set; } = string.Empty;
    }
}