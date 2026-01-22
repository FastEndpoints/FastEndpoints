using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NativeAotChecker.Tests;

public class HelloEndpointTests : IAsyncLifetime
{
    private DistributedApplication? _app;

    public async ValueTask InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Program>();
        _app = await appHost.BuildAsync();
        await _app.StartAsync();

        // Wait for the API resource to be ready
        var resourceNotificationService = _app.Services.GetRequiredService<ResourceNotificationService>();
        await resourceNotificationService.WaitForResourceAsync("api", KnownResourceStates.Running);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app != null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    [Fact]
    public async Task GetHello_ReturnsHelloWorld()
    {
        // Arrange
        using var httpClient = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5050") };

        // Act
        var response = await httpClient.GetAsync("/hello");

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello World", content);
    }
}
