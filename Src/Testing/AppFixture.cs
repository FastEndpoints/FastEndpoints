using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Bogus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using Microsoft.AspNetCore.Http;

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

    // ReSharper disable VirtualMemberNeverOverridden.Global

    /// <summary>
    /// this will be called before the WAF is initialized. override this method if you'd like to do something before the WAF is initialized that is going to
    /// contribute to
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
        o ??= new();
        var client = _app.CreateDefaultClient(o.BaseAddress, o.CreateHandlers());
        c(client);

        return client;
    }

    /// <summary>
    /// create a http message handler for the underlying web host/test server
    /// </summary>
    public HttpMessageHandler CreateHandler(Action<HttpContext>? c = null)
        => c is null ? _app.Server.CreateHandler() : _app.Server.CreateHandler(c);

    async ValueTask IAsyncLifetime.InitializeAsync()
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
                    b.ConfigureTestServices(ConfigureServices);
                    ConfigureApp(b);
                });
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
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