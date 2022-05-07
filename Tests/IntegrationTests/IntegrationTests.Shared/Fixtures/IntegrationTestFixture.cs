using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Shared.Fixtures;

public class IntegrationTestFixture : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public IntegrationTestFixture()
    {
        //hack for being able to run integration tests in azuer/github containers
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");

        // Ref: https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-6.0#basic-tests-with-the-default-webapplicationfactory
        _factory = new CustomWebApplicationFactory<Program>();
    }

    public IServiceProvider ServiceProvider => _factory.Services;

    public ILogger<IntegrationTestFixture> Logger =>
        ServiceProvider.GetRequiredService<ILogger<IntegrationTestFixture>>();

    public IConfiguration Configuration => _factory.Configuration;

    public void SetOutputHelper(ITestOutputHelper outputHelper)
    {
        _factory.OutputHelper = outputHelper;
        var loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        loggerFactory.AddXUnit(outputHelper);
    }

    public IHttpContextAccessor HttpContextAccessor =>
        ServiceProvider.GetRequiredService<IHttpContextAccessor>();

    public HttpClient CreateNewClient(Action<IServiceCollection>? services = null)
    {
        return _factory.WithWebHostBuilder(b =>
             b.ConfigureTestServices(sv =>
             {
                 services?.Invoke(sv);
             }))
         .CreateClient();
    }

    public void RegisterTestServices(Action<IServiceCollection> services)
    {
        _factory.TestRegistrationServices = services;
    }

    public async Task ExecuteScopeAsync(Func<IServiceProvider, Task> action)
    {
        using var scope = ServiceProvider.CreateScope();

        await action(scope.ServiceProvider);
    }

    public async Task<T> ExecuteScopeAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var scope = ServiceProvider.CreateScope();

        var result = await action(scope.ServiceProvider);

        return result;
    }

    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    public virtual Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }
}