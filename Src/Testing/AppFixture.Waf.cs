using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

    protected sealed class AsyncLazy<T>(Func<Task<T>> taskFactory) : Lazy<Task<T>>(() => Task.Factory.StartNew(taskFactory).Unwrap())
    {
        public TaskAwaiter<T> GetAwaiter() //this signature allows this instance to be awaited directly
            => Value.GetAwaiter();
    }
}

/// <summary>
/// inherit this class to create a class fixture for an implementation of <see cref="TestBase{TFixture}" />.
/// </summary>
/// <typeparam name="TProgram">
/// the type of the web application to bootstrap via <see cref="WebApplicationFactory{TEntryPoint}" /> for regular testing or, to
/// publish and spin up an out of process black-box instance for testing a native aot build.
/// </typeparam>
public abstract partial class AppFixture<TProgram> : BaseFixture, IAsyncLifetime where TProgram : class
{
    /// <summary>
    /// the service provider of the bootstrapped web application
    /// </summary>
    /// <exception cref="InvalidOperationException">thrown if accessed during native aot black-box testing.</exception>
    public IServiceProvider Services
        => _wafInstance?.Services ?? throw new InvalidOperationException("Services are only available when using WAF. Disable NativeAot mode to access Services.");

    /// <summary>
    /// the test server of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
    /// </summary>
    /// <exception cref="InvalidOperationException">thrown if accessed during native aot black-box testing.</exception>
    public TestServer Server => _wafInstance?.Server ?? throw new InvalidOperationException("Server is only available when using WAF. Disable NativeAot mode to access Server.");

    /// <summary>
    /// the default http client
    /// </summary>
    public HttpClient Client { get; set; } = null!;

    WebApplicationFactory<TProgram>? _wafInstance;
    readonly ConcurrentBag<HttpClient> _aotClients = [];
    bool _isAotMode;

    // ReSharper disable VirtualMemberNeverOverridden.Global
    /// <summary>
    /// this will be called before the WAF is initialized. override this method if you'd like to do something before the WAF is initialized that is going to contribute to
    /// the creation of the WAF, such as initialization of a 'TestContainer'.
    /// <para>NOTE: only executed when running in WAF mode.</para>
    /// </summary>
    protected virtual ValueTask PreSetupAsync()
        => ValueTask.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time setup for the fixture.
    /// it is run before any of the test-methods of the class is executed, but after the WAF/black-box is initialized.
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
    /// override this method if you'd like to provide any configuration for the generic app host of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
    /// <para>NOTE: only executed when running in WAF mode.</para>
    /// </summary>
    protected virtual IHost ConfigureAppHost(IHostBuilder a)
    {
        var host = a.Build();
        host.Start();

        return host;
    }

    /// <summary>
    /// override this method if you'd like to provide any configuration for the web host of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
    /// <para>NOTE: only executed when running in WAF mode.</para>
    /// </summary>
    protected virtual void ConfigureApp(IWebHostBuilder a) { }

    /// <summary>
    /// override this method if you'd like to override (remove/replace) any services registered in the underlying web application's DI container.
    /// <para>NOTE: only executed when running in WAF mode.</para>
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
            _aotClients.Add(client);

            return client;
        }

        o ??= new();
        var wafClient = _wafInstance!.CreateDefaultClient(o.BaseAddress, o.CreateHandlers());
        c(wafClient);

        return wafClient;
    }

    /// <summary>
    /// create an http message handler for the underlying web host/test server
    /// </summary>
    /// <exception cref="InvalidOperationException">thrown if accessed during native aot black-box testing.</exception>
    public HttpMessageHandler CreateHandler(Action<HttpContext>? c = null)
    {
        if (_isAotMode)
            throw new InvalidOperationException("CreateHandler is only available when using WAF. Disable NativeAot mode to use the test server handler.");

        return c is null
                   ? _wafInstance!.Server.CreateHandler()
                   : _wafInstance!.Server.CreateHandler(c);
    }

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        _isAotMode = IsNativeAotTestMode();

        if (_isAotMode)
        {
            await InitializeAotAsync();
            await SetupAsync();

            return;
        }

        var tDerivedFixture = GetType();

        if (tDerivedFixture.IsDefined(typeof(DisableWafCacheAttribute), true))
            _wafInstance = (WebApplicationFactory<TProgram>)await WafInitializer();
        else
            _wafInstance = (WebApplicationFactory<TProgram>)await WafCache.GetOrAdd(tDerivedFixture, _ => new(WafInitializer));

        Client = _wafInstance.CreateClient();

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
            foreach (var client in _aotClients)
                client.Dispose();
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
}