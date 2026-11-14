using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace EventBus;

public class EventBusMetadataTests
{
    [Fact]
    public async Task Catalog_Snapshots_Contracts_And_Handler_Lists_Before_Dispatch()
    {
        HashSet<Type> handlers = [typeof(FirstHandler), typeof(SecondHandler)];
        Dictionary<Type, HashSet<Type>> discovery = new() { [typeof(BaseEvent)] = handlers };
        var catalog = new EventCatalog(discovery);

        handlers.Clear();
        discovery.Clear();
        discovery.Add(typeof(DerivedEvent), [typeof(DerivedHandler)]);

        var exposedHandlers = (IList<Type>)catalog.GetHandlers(typeof(BaseEvent));
        Should.Throw<NotSupportedException>(() => exposedHandlers[0] = typeof(DerivedHandler));
        Should.Throw<NotSupportedException>(() => ((IList<Type>)catalog.Contracts).Clear());

        using var provider = new ServiceCollection().AddEventServices(_ => catalog).BuildServiceProvider();
        provider.GetRequiredService<EventOptions>().EnableInheritance = true;
        var model = new DerivedEvent();

        await provider.GetRequiredService<EventBus<DerivedEvent>>().PublishAsync(model);

        model.Calls.ShouldBe(["first", "second"]);
        catalog.GetHandlers(typeof(DerivedEvent)).ShouldBeEmpty();
        EventCatalog.Empty.Contracts.ShouldBeEmpty();
        EventCatalog.Empty.GetHandlers(typeof(BaseEvent)).ShouldBeEmpty();
    }

    [Fact]
    public void Registration_Index_Is_Immutable_And_Does_Not_Activate_Factories()
    {
        var activations = 0;
        var services = new ServiceCollection();
        services.AddSingleton<IEventHandler<BaseEvent>>(_ =>
        {
            activations++;
            return new FirstHandler();
        });
        services.AddSingleton<IEnumerable<IEventHandler<DerivedEvent>>>(_ =>
        {
            activations++;
            return [];
        });
        services.AddKeyedSingleton<IEventHandler<OtherEvent>>("ignored", (_, _) =>
        {
            activations++;
            return new OpenHandler<OtherEvent>();
        });
        services.AddTransient(typeof(IEventHandler<>), typeof(OpenHandler<>));

        var index = new EventRegistrationIndex(services);

        // No provider is built here. The index must copy metadata rather than retain the collection.
        services.Clear();
        services.AddSingleton<IEventHandler<OtherEvent>, OpenHandler<OtherEvent>>();
        Should.Throw<NotSupportedException>(() => ((ICollection<Type>)index.Contracts).Add(typeof(OtherEvent)));

        activations.ShouldBe(0);
        index.Contracts.Length.ShouldBe(2);
        index.Contracts.ShouldContain(typeof(BaseEvent));
        index.Contracts.ShouldContain(typeof(DerivedEvent));
        index.HasOpenHandlers.ShouldBeTrue();
    }

    public class BaseEvent
    {
        public List<string> Calls { get; } = [];
    }

    public sealed class DerivedEvent : BaseEvent;
    public sealed class OtherEvent;

    public sealed class FirstHandler : IEventHandler<BaseEvent>
    {
        public Task HandleAsync(BaseEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Add("first");
            return Task.CompletedTask;
        }
    }

    public sealed class SecondHandler : IEventHandler<BaseEvent>
    {
        public Task HandleAsync(BaseEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Add("second");
            return Task.CompletedTask;
        }
    }

    public sealed class DerivedHandler : IEventHandler<DerivedEvent>
    {
        public Task HandleAsync(DerivedEvent eventModel, CancellationToken ct)
        {
            eventModel.Calls.Add("derived");
            return Task.CompletedTask;
        }
    }

    public sealed class OpenHandler<T> : IEventHandler<T>
    {
        public Task HandleAsync(T eventModel, CancellationToken ct) => Task.CompletedTask;
    }
}
