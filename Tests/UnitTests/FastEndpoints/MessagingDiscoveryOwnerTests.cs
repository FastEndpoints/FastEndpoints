using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Shouldly;
using Xunit;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class MessagingDiscoveryOwnerTests : IDisposable
{
    readonly EventBusIsolationScope _isolation = new();

    public void Dispose() => _isolation.Dispose();

    [Theory, InlineData(true), InlineData(false)]
    public void AddFastEndpoints_Owns_Commands_And_Events_When_Both_Registration_Apis_Run(bool addMessagingFirst)
    {
        var services = new ServiceCollection();
        if (addMessagingFirst)
        {
            services.AddMessaging([typeof(MessagingCommandHandler), typeof(MessagingEventHandler)]);
            AddFastEndpoints(services);
        }
        else
        {
            AddFastEndpoints(services);
            services.AddMessaging([typeof(MessagingCommandHandler), typeof(MessagingEventHandler)]);
        }
        using var provider = services.BuildServiceProvider();
        provider.UseMessaging();

        var commands = provider.GetRequiredService<CommandHandlerRegistry>();
        commands.Keys.ShouldBe([typeof(FeCommand)]);
        commands.ShouldBeSameAs(provider.GetRequiredService<MessagingDiscovery>().Commands);

        provider.GetRequiredService<EventRegistry>()
                .ResolveDiscoveredHandlers<OwnerEvent>()
                .Select(h => h.GetType())
                .ShouldBe([typeof(FeEventHandler)]);
    }

    static void AddFastEndpoints(IServiceCollection services)
        => services.AddFastEndpoints([typeof(OwnerStubEndpoint), typeof(FeCommandHandler), typeof(FeEventHandler)]);

    public sealed class OwnerStubEndpoint : EndpointWithoutRequest
    {
        public override void Configure() { Get("messaging-discovery-owner-unit"); AllowAnonymous(); }
        public override Task HandleAsync(CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class FeCommand : ICommand;
    public sealed class MessagingCommand : ICommand;
    public sealed class OwnerEvent : IEvent;

    public sealed class FeCommandHandler : ICommandHandler<FeCommand>
    {
        public Task ExecuteAsync(FeCommand command, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class MessagingCommandHandler : ICommandHandler<MessagingCommand>
    {
        public Task ExecuteAsync(MessagingCommand command, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class FeEventHandler : IEventHandler<OwnerEvent>
    {
        public Task HandleAsync(OwnerEvent eventModel, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class MessagingEventHandler : IEventHandler<OwnerEvent>
    {
        public Task HandleAsync(OwnerEvent eventModel, CancellationToken ct) => Task.CompletedTask;
    }
}
