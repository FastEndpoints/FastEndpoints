using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Shared.Fixtures;

public class EndToEndTestFixture : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory<Program> _factory;

    public EndToEndTestFixture()
    {
        // Ref: https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-6.0#basic-tests-with-the-default-webapplicationfactory
        _factory = new CustomWebApplicationFactory<Program>();
        Environment.SetEnvironmentVariable("DOTNET_hostBuilder:reloadConfigOnChange", "false");
    }

    public IServiceProvider ServiceProvider => _factory.Services;

    public void SetOutputHelper(ITestOutputHelper outputHelper)
    {
        _factory.OutputHelper = outputHelper;
        var loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        loggerFactory.AddXUnit(outputHelper);
    }

    public HttpClient CreateNewClient(Action<IServiceCollection>? services = null) =>
        _factory.WithWebHostBuilder(b =>
                b.ConfigureTestServices(sv =>
                {
                    services?.Invoke(sv);
                }))
            .CreateClient();

    public void RegisterTestServices(Action<IServiceCollection> services) =>
        _factory.TestRegistrationServices = services;

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