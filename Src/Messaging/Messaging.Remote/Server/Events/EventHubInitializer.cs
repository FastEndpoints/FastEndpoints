using Microsoft.Extensions.Hosting;

namespace FastEndpoints;

sealed class EventHubInitializer : IHostedService
{
    //ct passed in here is useless. it's a default token: https://source.dot.net/#Microsoft.AspNetCore.Hosting/Internal/WebHost.cs,124
    public async Task StartAsync(CancellationToken _)
        => await EventHubBase.InitializeHubs();

    public Task StopAsync(CancellationToken _)
        => Task.CompletedTask;
}
