using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Bogus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#endif

namespace FastEndpoints.Testing;

/// <summary>
/// base class for <see cref="AppFixture{TProgram}" />.
/// </summary>
public abstract class BaseFixture : IFaker
{
    static readonly Faker _faker = new();

    /// <inheritdoc />
    public Faker Fake => _faker;

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
    public IServiceProvider Services => _app.Services;

    /// <summary>
    /// the test server of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
    /// </summary>
    public TestServer Server => _app.Server;

    /// <summary>
    /// the default http client
    /// </summary>
    public HttpClient Client { get; set; } = null!;

    WebApplicationFactory<TProgram> _app = null!;
    readonly IMessageSink? _messageSink;
    readonly ITestOutputHelper? _outputHelper;

    //reason for ctor overloads: https://github.com/FastEndpoints/FastEndpoints/pull/548
    protected AppFixture(IMessageSink s)
    {
        _messageSink = s;
    }

    protected AppFixture(ITestOutputHelper h)
    {
        _outputHelper = h;
    }

    protected AppFixture(IMessageSink s, ITestOutputHelper h)
    {
        _messageSink = s;
        _outputHelper = h;
    }

    protected AppFixture() { }

    // ReSharper disable VirtualMemberNeverOverridden.Global

    /// <summary>
    /// this will be called before the WAF is initialized. override this method if you'd like to do something before the WAF is initialized that is going to contribute to
    /// the creation of the WAF, such as initialization of a 'TestContainer'.
    /// </summary>
    protected virtual Task PreSetupAsync()
        => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time setup for the fixture.
    /// it is run before any of the test-methods of the class is executed, but after the WAF is initialized.
    /// </summary>
    protected virtual Task SetupAsync()
        => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time teardown for the fixture.
    /// it is run after all test-methods have executed.
    /// </summary>
    protected virtual Task TearDownAsync()
        => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to provide any configuration for the generic app host of the underlying <see cref="WebApplicationFactory{TEntryPoint}" />
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
        o ??= new();
        var client = _app.CreateDefaultClient(o.BaseAddress, o.CreateHandlers());
        c(client);

        return client;
    }

    /// <summary>
    /// create a http message handler for the underlying web host/test server
    /// </summary>
#if NET7_0_OR_GREATER
    public HttpMessageHandler CreateHandler(Action<HttpContext>? c = null)
        => c is null ? _app.Server.CreateHandler() : _app.Server.CreateHandler(c);
#else
    public HttpMessageHandler CreateHandler()
        => _app.Server.CreateHandler();
#endif

    async Task IAsyncLifetime.InitializeAsync()
    {
        var tDerivedFixture = GetType();

        if (tDerivedFixture.IsDefined(typeof(DisableWafCacheAttribute), true))
            _app = (WebApplicationFactory<TProgram>)await WafInitializer();
        else
            _app = (WebApplicationFactory<TProgram>)await WafCache.GetOrAdd(tDerivedFixture, _ => new(WafInitializer));

        Client = _app.CreateClient();

        await SetupAsync();

        async Task<object> WafInitializer()
        {
            await PreSetupAsync();

            return new WafWrapper(ConfigureAppHost).WithWebHostBuilder(
                b =>
                {
                    b.UseEnvironment("Testing");
                    b.ConfigureLogging(
                        l =>
                        {
                            l.ClearProviders();
                            if (_messageSink is not null)
                                l.AddXUnit(_messageSink);
                            if (_outputHelper is not null)
                                l.AddXUnit(_outputHelper);
                        });
                    b.ConfigureTestServices(ConfigureServices);
                    ConfigureApp(b);
                });
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await TearDownAsync();

        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        Client?.Dispose();
    }

    class WafWrapper(Func<IHostBuilder, IHost> configureAppHost) : WebApplicationFactory<TProgram>
    {
        protected override IHost CreateHost(IHostBuilder builder)
            => configureAppHost(builder);
    }
}

//TODO: remove this class at v6.0
[Obsolete("Use 'AppFixture<TProgram>' abstract class instead of this class going forward.", false)]
public abstract class TestFixture<TProgram> : AppFixture<TProgram> where TProgram : class
{
    protected TestFixture(IMessageSink s) : base(s) { }

    protected TestFixture(ITestOutputHelper h) : base(h) { }

    protected TestFixture(IMessageSink s, ITestOutputHelper h) : base(s, h) { }

    protected TestFixture() { }
}