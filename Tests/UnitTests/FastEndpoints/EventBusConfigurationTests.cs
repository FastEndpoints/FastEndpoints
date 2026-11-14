using System.Text.Json;
using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RouteMapper;
using Shouldly;
using Xunit;
using static EventBus.EventBusInheritanceTests;

namespace EventBus;

[Collection(RouteMapperCollection.Name)]
public class EventBusConfigurationTests
{
    [Fact]
    public async Task Second_Host_Configures_Events_After_First_Host_Serialization_Without_Replaying_Config()
    {
        var previousResolver = ServiceResolver.InstanceNotSet ? null : ServiceResolver.Instance;
        var previousContext = new HttpContextAccessor().HttpContext;
        var previousSerializer = Config.SerOpts.Options;
        var previousConfigured = MainExtensions.SerializerConfigured;
        var previousFilter = Config.EpOpts.Filter;
        var previousConfigurator = Config.EpOpts.Configurator;
        var previousWarmup = Config.EpOpts.WarmupRequested;
        var previousPropertyResolver = FluentValidation.ValidatorOptions.Global.PropertyNameResolver;
        try
        {
            new HttpContextAccessor().HttpContext = null;
            MainExtensions.SerializerConfigured = false;
            Config.SerOpts.Options = new();
            Config.EpOpts.Filter = null;
            Config.EpOpts.Configurator = null;
            Config.EpOpts.WarmupRequested = false;
            var callbacks = 0;
            var firstBuilder = WebApplication.CreateBuilder();
            firstBuilder.Services.AddFastEndpoints([typeof(StubEndpoint), typeof(Dual)]);
            await using var first = firstBuilder.Build();
            first.Services.UseMessaging(o => o.Events.EnableInheritance = false);
            first.UseFastEndpoints(_ => callbacks++);
            _ = JsonSerializer.Serialize(new { Answer = 42 }, Config.SerOpts.Options);
            Config.SerOpts.Options.IsReadOnly.ShouldBeTrue();
            var serializedOptions = Config.SerOpts.Options;

            var secondBuilder = WebApplication.CreateBuilder();
            secondBuilder.Services.AddFastEndpoints([typeof(StubEndpoint), typeof(Dual)]);
            await using var second = secondBuilder.Build();
            second.Services.UseMessaging(o => o.Events.EnableInheritance = true);
            second.UseFastEndpoints(_ => callbacks++);

            callbacks.ShouldBe(1);
            Config.SerOpts.Options.ShouldBeSameAs(serializedOptions);
            var model = new Derived();
            await second.Services.GetRequiredService<EventBus<Base>>().PublishAsync(model);
            model.Calls.Order().ShouldBe(new[] { "base", "derived" }.Order());
            model.Calls.Clear();
            await first.Services.GetRequiredService<EventBus<Base>>().PublishAsync(model);
            model.Calls.ShouldBe(["base"]);
        }
        finally
        {
            ServiceResolver.Instance = previousResolver!;
            new HttpContextAccessor().HttpContext = previousContext;
            Config.SerOpts.Options = previousSerializer;
            MainExtensions.SerializerConfigured = previousConfigured;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
            Config.EpOpts.WarmupRequested = previousWarmup;
            FluentValidation.ValidatorOptions.Global.PropertyNameResolver = previousPropertyResolver;
        }
    }
}
