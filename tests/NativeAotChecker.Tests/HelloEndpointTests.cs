using Aspire.Hosting.Testing;
using Xunit;

namespace NativeAotChecker.Tests;

public class HelloEndpointTests : IAsyncLifetime
{
    private DistributedApplicationFactory? _appFactory;

    public async ValueTask InitializeAsync()
    {
        _appFactory = new DistributedApplicationFactory(typeof(Program));
        await _appFactory.StartAsync();

        // Give the API time to start
        await Task.Delay(2000);
    }

    public async ValueTask DisposeAsync()
    {
        if (_appFactory != null)
        {
            await _appFactory.DisposeAsync();
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
