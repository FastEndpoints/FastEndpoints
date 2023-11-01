using Bogus;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Xunit;
using Xunit.Abstractions;
#if NET7_0_OR_GREATER
using Microsoft.AspNetCore.Http;
#endif

namespace FastEndpoints.Testing;

public abstract class BaseFixture
{
    protected static readonly Faker _faker = new();
    protected static readonly ConcurrentDictionary<Type, object> AppCache = new();
}

/// <summary>
/// inherit this class to create a class fixture for an implementation of <see cref="TestClass{TFixture}" />.
/// </summary>
/// <typeparam name="TProgram">the type of the web application to bootstrap via <see cref="WebApplicationFactory{TEntryPoint}" /></typeparam>
public abstract class TestFixture<TProgram> : BaseFixture, IAsyncLifetime, IFixture where TProgram : class
{
    /// <inheritdoc />
    public Faker Fake => _faker;

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
    public HttpClient Client { get; set; }

    readonly WebApplicationFactory<TProgram> _app;

    protected TestFixture(IMessageSink s)
    {
        _app = (WebApplicationFactory<TProgram>)
            AppCache.GetOrAdd(
                GetType(),
                _ => new WebApplicationFactory<TProgram>().WithWebHostBuilder(
                    b =>
                    {
                        b.UseEnvironment("Testing");
                        b.ConfigureLogging(l => l.ClearProviders().AddXUnit(s));
                        b.ConfigureTestServices(ConfigureServices);
                        ConfigureApp(b);
                    }));
        Client = _app.CreateClient();
    }

    /// <summary>
    /// override this method if you'd like to do some one-time setup for the test-class.
    /// it is run before any of the test-methods of the class is executed.
    /// </summary>
    protected virtual Task SetupAsync()
        => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do some one-time teardown for the test-class.
    /// it is run after all test-methods have executed.
    /// </summary>
    protected virtual Task TearDownAsync()
        => Task.CompletedTask;

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
    public HttpClient CreateClient(WebApplicationFactoryClientOptions? o = null)
        => CreateClient(_ => { }, o);

    /// <summary>
    /// create a client for the underlying web application
    /// </summary>
    /// <param name="c">configuration action for the client</param>
    /// <param name="o">optional client options for the WAF</param>
    public HttpClient CreateClient(Action<HttpClient> c, WebApplicationFactoryClientOptions? o = null)
    {
        var client = o is null ? _app.CreateClient() : _app.CreateClient(o);
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

    public Task InitializeAsync()
        => SetupAsync();

    public virtual Task DisposeAsync()
    {
        Client.Dispose();

        return TearDownAsync();
    }
}