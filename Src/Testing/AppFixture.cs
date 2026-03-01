using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using Bogus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FastEndpoints.Testing;

/// <summary>
/// base class for <see cref="AppFixture{TProgram}" />.
/// </summary>
public abstract class BaseFixture : IFaker
{
    static readonly Faker _faker = new();

    public Faker Fake => _faker;

#pragma warning disable CA1822
    public ITestContext Context => TestContext.Current;
    public CancellationToken Cancellation => TestContext.Current.CancellationToken;
#pragma warning restore CA1822

    //we're using an async friendly lazy wrapper to ensure that no more than 1 waf instance is ever created per derived AppFixture type in a high concurrency situation.
    protected static readonly ConcurrentDictionary<Type, AsyncLazy<object>> WafCache = new();

    protected sealed class AsyncLazy<T>(Func<Task<T>> taskFactory)
        : Lazy<Task<T>>(() => Task.Factory.StartNew(taskFactory).Unwrap())
    {
        public TaskAwaiter<T> GetAwaiter() //this signature allows this instance to be awaited directly
            => Value.GetAwaiter();
    }
}

/// <summary>
/// inherit this class to create a class fixture for an implementation of <see cref="TestBase{TFixture}" />.
/// </summary>
/// <typeparam name="TProgram">the type of the web application to bootstrap via <see cref="WebApplicationFactory{TEntryPoint}" /></typeparam>
public abstract class AppFixture<TProgram> : BaseFixture, IAsyncLifetime where TProgram : class
{
    /// <summary>
    /// the service provider of the bootstrapped web application
    /// </summary>
    public IServiceProvider Services
        => _app?.Services ?? throw new InvalidOperationException("Services are only available when using WAF. Disable NativeAot mode to access Services.");

    /// <summary>
    /// the test server of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
    /// </summary>
    public TestServer Server => _app?.Server ?? throw new InvalidOperationException("Server is only available when using WAF. Disable NativeAot mode to access Server.");

    /// <summary>
    /// the default http client
    /// </summary>
    public HttpClient Client { get; set; } = null!;

    WebApplicationFactory<TProgram>? _app;
    AotSharedState? _aotState;
    string? _aotCacheKey;
    bool _isAotMode;
    static readonly ConcurrentDictionary<string, AsyncLazy<AotSharedState>> _aotCache = new();

    // ReSharper disable VirtualMemberNeverOverridden.Global
    /// <summary>
    /// this will be called before the WAF is initialized. it is only invoked for WAF-based tests. override this method if you'd like to do something before
    /// the WAF is initialized that is going to contribute to
    /// the creation of the WAF, such as initialization of a 'TestContainer'.
    /// </summary>
    protected virtual ValueTask PreSetupAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time setup for the fixture.
    /// it is run before any of the test-methods of the class is executed, but after the WAF is initialized.
    /// </summary>
    protected virtual ValueTask SetupAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time teardown for the fixture.
    /// it is run after all test-methods have executed.
    /// </summary>
    protected virtual ValueTask TearDownAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to provide any configuration for the generic app host of the underlying
    /// <see cref="WebApplicationFactory{TEntryPoint}" />
    /// </summary>
    protected virtual IHost ConfigureAppHost(IHostBuilder a)
    {
        var host = a.Build();
        host.Start();

        return host;
    }

    /// <summary>
    /// override this method if you'd like to provide any configuration for the web host of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
    /// />
    /// </summary>
    protected virtual void ConfigureApp(IWebHostBuilder a) { }

    /// <summary>
    /// override this method if you'd like to override (remove/replace) any services registered in the underlying web application's DI container.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection s) { }

    /// <summary>
    /// create a client for the underlying web application
    /// </summary>
    /// <param name="o">optional client options for the WAF</param>
    public HttpClient CreateClient(ClientOptions? o = null)
        => CreateClient(_ => { }, o);

    /// <summary>
    /// create a client for the underlying web application
    /// </summary>
    /// <param name="c">configuration action for the client</param>
    /// <param name="o">optional client options for the WAF</param>
    public HttpClient CreateClient(Action<HttpClient> c, ClientOptions? o = null)
    {
        if (_isAotMode)
        {
            var client = CreateAotClient();
            c(client);

            return client;
        }

        o ??= new();
        var wafClient = _app!.CreateDefaultClient(o.BaseAddress, o.CreateHandlers());
        c(wafClient);

        return wafClient;
    }

    /// <summary>
    /// create an http message handler for the underlying web host/test server
    /// </summary>
    public HttpMessageHandler CreateHandler(Action<HttpContext>? c = null)
    {
        if (_isAotMode)
            throw new InvalidOperationException("CreateHandler is only available when using WAF. Disable NativeAot mode to use the test server handler.");

        return c is null ? _app!.Server.CreateHandler() : _app!.Server.CreateHandler(c);
    }

    // ReSharper disable once UnusedParameter.Global
    /// <summary>
    /// override this method to configure the native aot target settings.
    /// </summary>
    protected virtual void ConfigureAotTarget(AotTargetOptions options) { }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        _isAotMode = IsNativeAotTestMode();

        if (_isAotMode)
        {
            var opts = new AotTargetOptions();
            ConfigureAotTarget(opts);

            var exePath = opts.ExePath;
            var baseUrl = opts.BaseUrl;

            if (string.IsNullOrWhiteSpace(exePath))
                throw new InvalidOperationException("NativeAot is enabled but no executable path was provided. Override ConfigureAotTarget() to set ExePath.");

            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("NativeAot is enabled but no base url was provided. Override ConfigureAotTarget() to set BaseUrl.");

            exePath = Path.GetFullPath(exePath);
            baseUrl = new Uri(baseUrl, UriKind.Absolute).ToString();
            _aotCacheKey = $"{exePath}|{baseUrl}";
            _aotState = await _aotCache.GetOrAdd(_aotCacheKey, _ => new(() => StartAotAsync(exePath, baseUrl, opts.HealthEndpointPath)));
            _aotState.AddRef();
            Client = CreateAotClient();
            await SetupAsync();

            return;
        }

        var tDerivedFixture = GetType();

        if (tDerivedFixture.IsDefined(typeof(DisableWafCacheAttribute), true))
            _app = (WebApplicationFactory<TProgram>)await WafInitializer();
        else
            _app = (WebApplicationFactory<TProgram>)await WafCache.GetOrAdd(tDerivedFixture, _ => new(WafInitializer));

        Client = _app.CreateClient();

        await SetupAsync();

        return;

        async Task<object> WafInitializer()
        {
            await PreSetupAsync();

            return new WafWrapper(ConfigureAppHost).WithWebHostBuilder(
                b =>
                {
                    b.UseEnvironment("Testing");
                    b.ConfigureTestServices(ConfigureServices);
                    ConfigureApp(b);
                });
        }
    }

    [SuppressMessage("ReSharper", "ConditionalAccessQualifierIsNonNullableAccordingToAPIContract")]
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        await TearDownAsync();

        if (_isAotMode)
        {
            Client?.Dispose();
            if (_aotState is not null)
                await ReleaseAotStateAsync(_aotCacheKey, _aotState);
        }
        else
            Client?.Dispose();
    }

    class WafWrapper(Func<IHostBuilder, IHost> configureAppHost) : WebApplicationFactory<TProgram>
    {
        protected override IHost CreateHost(IHostBuilder builder)
            => configureAppHost(builder);
    }

    static bool IsNativeAotTestMode()
    {
        if (AppContext.TryGetSwitch("NativeAotTestMode", out var enabled))
            return enabled;

        var data = AppContext.GetData("NativeAotTestMode");

        if (data is bool boolValue)
            return boolValue;

        return data is string stringValue && bool.TryParse(stringValue, out var parsed) && parsed;
    }

    static async Task<AotSharedState> StartAotAsync(string exePath, string baseUrl, string? healthPath)
    {
        if (!File.Exists(exePath))
            throw new FileNotFoundException("AOT executable not found. Run publish/build to generate it.", exePath);

        var baseUri = new Uri(baseUrl, UriKind.Absolute);
        var workingDir = Path.GetDirectoryName(exePath)!;

        var state = new AotSharedState(baseUri, exePath, workingDir, healthPath);
        state.StartProcess();
        await state.WaitForReadyAsync();

        return state;
    }

    HttpClient CreateAotClient()
        => new() { BaseAddress = _aotState!.BaseAddress };

    static async ValueTask ReleaseAotStateAsync(string? cacheKey, AotSharedState state)
    {
        if (state.Release())
        {
            if (!string.IsNullOrWhiteSpace(cacheKey))
                _aotCache.TryRemove(cacheKey, out _);

            await state.DisposeAsync();
        }
    }

    protected sealed class AotTargetOptions
    {
        /// <summary>
        /// the full path to the native aot executable.
        /// </summary>
        public string? ExePath { get; set; }

        /// <summary>
        /// the base address (host/port) to bind the aot app to.
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// the relative health endpoint path used to detect readiness (default: /healthy).
        /// </summary>
        public string? HealthEndpointPath { get; set; } = "/healthy";
    }

    sealed class AotSharedState(Uri baseAddress, string exePath, string workingDir, string? healthPath)
    {
        readonly StringBuilder _processOutput = new();
        Process? _process;
        int _refCount;

        public Uri BaseAddress { get; } = baseAddress;

        readonly string _healthPath = string.IsNullOrWhiteSpace(healthPath) ? "/healthy" : healthPath;

        public void AddRef()
            => Interlocked.Increment(ref _refCount);

        public bool Release()
            => Interlocked.Decrement(ref _refCount) == 0;

        public void StartProcess()
        {
            _processOutput.Clear();
            _process = new()
            {
                StartInfo = new()
                {
                    FileName = exePath,
                    Arguments = $"--urls={BaseAddress}",
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    WindowStyle = ProcessWindowStyle.Normal
                }
            };

            _process.OutputDataReceived += (_, e) =>
                                           {
                                               if (e.Data is not null)
                                                   _processOutput.AppendLine(e.Data);
                                           };
            _process.ErrorDataReceived += (_, e) =>
                                          {
                                              if (e.Data is not null)
                                                  _processOutput.AppendLine(e.Data);
                                          };
            _process.Start();
            _process.BeginErrorReadLine();
            _process.BeginOutputReadLine();
        }

        public async Task WaitForReadyAsync()
        {
            using var client = new HttpClient();
            client.BaseAddress = BaseAddress;
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < TimeSpan.FromSeconds(5))
            {
                if (_process?.HasExited == true)
                {
                    var output = _processOutput.ToString();

                    throw new InvalidOperationException($"AOT process exited unexpectedly with code {_process.ExitCode}.\n\nProcess output:\n{output}");
                }

                try
                {
                    var response = await client.GetAsync(_healthPath);

                    if (response.IsSuccessStatusCode)
                        return;
                }
                catch
                {
                    // do nothing
                }

                await Task.Delay(500);
            }

            var finalOutput = _processOutput.ToString();

            throw new InvalidOperationException($"AOT API failed to respond in time.\n\nProcess output:\n{finalOutput}");
        }

        public async ValueTask DisposeAsync()
        {
            if (_process is { HasExited: false })
            {
                _process.Kill();
                await _process.WaitForExitAsync();
            }
            _process?.Dispose();
        }
    }
}